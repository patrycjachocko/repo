using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace praca_dyplomowa_zesp.Migrations
{
    /// <inheritdoc />
    public partial class fix1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Guides_GuideId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_GuideId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "Author",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "CommentsCount",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "EstimatedReadTime",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "Likes",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "PrimaryImage",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "GuideId",
                table: "Comments");

            migrationBuilder.RenameColumn(
                name: "Views",
                table: "Guides",
                newName: "IgdbGameId");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "Guides",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "Tags",
                table: "Guides",
                newName: "UserId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Guides",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_Guides_IgdbGameId",
                table: "Guides",
                column: "IgdbGameId");

            migrationBuilder.CreateIndex(
                name: "IX_Guides_UserId",
                table: "Guides",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Guides_AspNetUsers_UserId",
                table: "Guides",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guides_AspNetUsers_UserId",
                table: "Guides");

            migrationBuilder.DropIndex(
                name: "IX_Guides_IgdbGameId",
                table: "Guides");

            migrationBuilder.DropIndex(
                name: "IX_Guides_UserId",
                table: "Guides");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Guides",
                newName: "Tags");

            migrationBuilder.RenameColumn(
                name: "IgdbGameId",
                table: "Guides",
                newName: "Views");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Guides",
                newName: "Version");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Guides",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "Guides",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Guides",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CommentsCount",
                table: "Guides",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "Guides",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedReadTime",
                table: "Guides",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Guides",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Guides",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Guides",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Likes",
                table: "Guides",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryImage",
                table: "Guides",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Guides",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GuideId",
                table: "Comments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_GuideId",
                table: "Comments",
                column: "GuideId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Guides_GuideId",
                table: "Comments",
                column: "GuideId",
                principalTable: "Guides",
                principalColumn: "Id");
        }
    }
}
