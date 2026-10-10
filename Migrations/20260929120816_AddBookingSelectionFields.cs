using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commute360.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingSelectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoardingStopId",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Days",
                table: "Bookings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DropOffStopId",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BoardingStopId",
                table: "Bookings",
                column: "BoardingStopId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_DropOffStopId",
                table: "Bookings",
                column: "DropOffStopId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Stops_BoardingStopId",
                table: "Bookings",
                column: "BoardingStopId",
                principalTable: "Stops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Stops_DropOffStopId",
                table: "Bookings",
                column: "DropOffStopId",
                principalTable: "Stops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Stops_BoardingStopId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Stops_DropOffStopId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BoardingStopId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_DropOffStopId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BoardingStopId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Days",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DropOffStopId",
                table: "Bookings");
        }
    }
}
