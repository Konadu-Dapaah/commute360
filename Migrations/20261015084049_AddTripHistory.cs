using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commute360.Migrations
{
    /// <inheritdoc />
    public partial class AddTripHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Boardings_Bookings_BookingId",
                table: "Boardings");

            migrationBuilder.DropForeignKey(
                name: "FK_Boardings_Users_DriverId",
                table: "Boardings");

            migrationBuilder.AlterColumn<int>(
                name: "DriverId",
                table: "Boardings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "Boardings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "AlightedAt",
                table: "Boardings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoardingStopName",
                table: "Boardings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropOffStopName",
                table: "Boardings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiderName",
                table: "Boardings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RouteId",
                table: "Boardings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouteName",
                table: "Boardings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Boardings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Boardings_Bookings_BookingId",
                table: "Boardings",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Boardings_Users_DriverId",
                table: "Boardings",
                column: "DriverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Boardings_Bookings_BookingId",
                table: "Boardings");

            migrationBuilder.DropForeignKey(
                name: "FK_Boardings_Users_DriverId",
                table: "Boardings");

            migrationBuilder.DropColumn(
                name: "AlightedAt",
                table: "Boardings");

            migrationBuilder.DropColumn(
                name: "BoardingStopName",
                table: "Boardings");

            migrationBuilder.DropColumn(
                name: "DropOffStopName",
                table: "Boardings");

            migrationBuilder.DropColumn(
                name: "RiderName",
                table: "Boardings");

            migrationBuilder.DropColumn(
                name: "RouteId",
                table: "Boardings");

            migrationBuilder.DropColumn(
                name: "RouteName",
                table: "Boardings");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Boardings");

            migrationBuilder.AlterColumn<int>(
                name: "DriverId",
                table: "Boardings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "Boardings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Boardings_Bookings_BookingId",
                table: "Boardings",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boardings_Users_DriverId",
                table: "Boardings",
                column: "DriverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
