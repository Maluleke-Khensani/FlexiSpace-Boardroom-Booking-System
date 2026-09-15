using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexiSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingModifiedAndCancelledBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_ApprovedById",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "ApprovedById",
                table: "Bookings",
                newName: "ModifiedById");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_ApprovedById",
                table: "Bookings",
                newName: "IX_Bookings_ModifiedById");

            migrationBuilder.AddColumn<int>(
                name: "CancelledById",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CancelledById",
                table: "Bookings",
                column: "CancelledById");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_CancelledById",
                table: "Bookings",
                column: "CancelledById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_ModifiedById",
                table: "Bookings",
                column: "ModifiedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_CancelledById",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_ModifiedById",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CancelledById",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancelledById",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "ModifiedById",
                table: "Bookings",
                newName: "ApprovedById");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_ModifiedById",
                table: "Bookings",
                newName: "IX_Bookings_ApprovedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_ApprovedById",
                table: "Bookings",
                column: "ApprovedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
