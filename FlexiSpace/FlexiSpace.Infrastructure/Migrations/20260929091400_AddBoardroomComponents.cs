using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexiSpace.Infrastructure.Migrations
{
    // Creates the BoardroomComponents table. The BoardroomComponent entity
    // (which links a combined boardroom to the boardrooms it's made of) was
    // added to the model without a migration, so the table has never been
    // created. BookingService's conflict check already queries it, so it is
    // needed before room blocking (which reuses that check) can work.
    // If your database already has this table, delete this migration
    // (both files) before running "dotnet ef database update".

    /// <inheritdoc />
    public partial class AddBoardroomComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardroomComponents",
                columns: table => new
                {
                    CombinedBoardroomId = table.Column<int>(type: "int", nullable: false),
                    ComponentBoardroomId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardroomComponents", x => new { x.CombinedBoardroomId, x.ComponentBoardroomId });
                    table.ForeignKey(
                        name: "FK_BoardroomComponents_Boardrooms_CombinedBoardroomId",
                        column: x => x.CombinedBoardroomId,
                        principalTable: "Boardrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BoardroomComponents_Boardrooms_ComponentBoardroomId",
                        column: x => x.ComponentBoardroomId,
                        principalTable: "Boardrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardroomComponents_ComponentBoardroomId",
                table: "BoardroomComponents",
                column: "ComponentBoardroomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardroomComponents");
        }
    }
}
