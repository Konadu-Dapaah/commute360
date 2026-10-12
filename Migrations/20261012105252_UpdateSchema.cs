using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commute360.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "BoardingCount",
                table: "Stops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DropOffCount",
                table: "Stops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BookedRidersCount",
                table: "Routes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BoardingsCount",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkipsCount",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BoardingCount",
                table: "Stops");

            migrationBuilder.DropColumn(
                name: "DropOffCount",
                table: "Stops");

            migrationBuilder.DropColumn(
                name: "BookedRidersCount",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "BoardingsCount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SkipsCount",
                table: "Bookings");
        }
    }
}
