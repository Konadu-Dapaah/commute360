using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Commute360.Data;
using Commute360.Services;
using Commute360.Hubs;
using Commute360.Models;
using Swashbuckle.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Bind to 0.0.0.0 so external requests work on Render. Falls back to port 5291 locally.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5291";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// 1. CORS Policy
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Configure Forwarded Headers for Render proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 2. Add controllers and JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Swagger / OpenAPI Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database & Application Services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddHttpClient();

// Brevo (HTTPS) email sender when BrevoApiKey is configured, otherwise fallback to SMTP
builder.Services.AddScoped<IEmailSender>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return !string.IsNullOrWhiteSpace(config["Email:BrevoApiKey"])
        ? ActivatorUtilities.CreateInstance<BrevoEmailSender>(sp)
        : ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp);
});

builder.Services.AddScoped<AccountMailer>();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();

// Rate limiting for authentication endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// 3. Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders();

// 4. Database Migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// 5. Global Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("Frontend");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 6. Map Endpoints
app.MapControllers();
app.MapHub<BusLocationHub>("/hubs/bus");
app.MapHealthChecks("/health");

// 7. Seed Initial Admin Account if Database is Empty
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();

        if (!db.Users.Any(u => u.Role == "Admin"))
        {
            var adminUser = new User
            {
                Name = "Ama",
                Email = "amaboakyedapaah@gmail.com",
                Role = "Admin",
                EmailVerified = true
            };

            var hasher = new PasswordHasher<User>();
            adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin123!");

            db.Users.Add(adminUser);
            db.SaveChanges();
            Console.WriteLine("--> Seeded default Admin account: amaboakyedapaah@gmail.com / Admin123!");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the admin user.");
    }
}

app.Run();