using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace praca_dyplomowa_zesp.Migrations
{
    /// <inheritdoc />
    public partial class migracja69 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Guides",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Guides",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Guides");
        }
    }
}
