using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace praca_dyplomowa_zesp.Migrations
{
    /// <inheritdoc />
    public partial class todolist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToDoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Content = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    GameInLibraryId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToDoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToDoItems_GamesInLibraries_GameInLibraryId",
                        column: x => x.GameInLibraryId,
                        principalTable: "GamesInLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToDoItems_GameInLibraryId",
                table: "ToDoItems",
                column: "GameInLibraryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToDoItems");
        }
    }
}
