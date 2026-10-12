using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Commute360.Migrations
{
    /// <inheritdoc />
    public partial class AddSendToToAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_RouteId",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_SkippedRides_BookingId",
                table: "SkippedRides");

            migrationBuilder.DropIndex(
                name: "IX_Boardings_BookingId",
                table: "Boardings");

            migrationBuilder.AddColumn<string>(
                name: "SendTo",
                table: "Announcements",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SosIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RouteId = table.Column<int>(type: "integer", nullable: false),
                    DriverId = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SosIncidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_RouteId_Status_CreatedAt",
                table: "WaitlistEntries",
                columns: new[] { "RouteId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SkippedRides_BookingId_RideDate",
                table: "SkippedRides",
                columns: new[] { "BookingId", "RideDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTokens_TokenHash",
                table: "EmailTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boardings_BookingId_RideDate",
                table: "Boardings",
                columns: new[] { "BookingId", "RideDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedAt",
                table: "Announcements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SosIncidents_ResolvedAt_CreatedAt",
                table: "SosIncidents",
                columns: new[] { "ResolvedAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SosIncidents");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_RouteId_Status_CreatedAt",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_SkippedRides_BookingId_RideDate",
                table: "SkippedRides");

            migrationBuilder.DropIndex(
                name: "IX_EmailTokens_TokenHash",
                table: "EmailTokens");

            migrationBuilder.DropIndex(
                name: "IX_Boardings_BookingId_RideDate",
                table: "Boardings");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_CreatedAt",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "SendTo",
                table: "Announcements");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_RouteId",
                table: "WaitlistEntries",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_SkippedRides_BookingId",
                table: "SkippedRides",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Boardings_BookingId",
                table: "Boardings",
                column: "BookingId");
        }
    }
}
