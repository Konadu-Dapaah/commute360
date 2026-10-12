using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.Services;

namespace Commute360.Hubs;

[Authorize] // Requires authenticated JWT connection
public class BusLocationHub : Hub
{
    private readonly AppDbContext _context;

    public BusLocationHub(AppDbContext context)
    {
        _context = context;
    }

    public async Task JoinRoute(string routeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, routeId);
    }

    public async Task LeaveRoute(string routeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, routeId);
    }

    public async Task SendLocation(string routeId, double lat, double lng)
    {
        // 1. Verify that the caller has the "Driver" or "Admin" role
        var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value 
                    ?? Context.User?.FindFirst("role")?.Value;

        if (userRole != "Driver" && userRole != "Admin")
        {
            throw new HubException("Unauthorized: Only assigned drivers can update bus location.");
        }

        // 2. Broadcast coordinates live to clients listening on this route
        await Clients.Group(routeId).SendAsync("ReceiveLocation", lat, lng);

        // 3. Check proximity to route stops
        if (int.TryParse(routeId, out int routeIdInt))
        {
            var stops = await _context.Stops
                .Where(s => s.RouteId == routeIdInt)
                .ToListAsync();

            foreach (var stop in stops)
            {
                double distance = DistanceService.CalculateDistanceKm(lat, lng, stop.Latitude, stop.Longitude);
                if (distance <= 1.0)
                {
                    await Clients.Group(routeId).SendAsync("BusNearStop", stop.Name, distance);
                }
            }
        }
    }
}