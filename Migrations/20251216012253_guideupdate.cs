using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace praca_dyplomowa_zesp.Migrations
{
    /// <inheritdoc />
    public partial class guideupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                table: "Guides",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Guides",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRejected",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Guides");
        }
    }
}
