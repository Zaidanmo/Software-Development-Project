using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Realworlddotnet.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConductStatisticMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReadCount",
                table: "Articles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "Timestamp",
                table: "ArticleFavorites",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SearchCount",
                columns: table => new
                {
                    KeywordId = table.Column<string>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchCount", x => x.KeywordId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchCount");

            migrationBuilder.DropColumn(
                name: "ReadCount",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "ArticleFavorites");
        }
    }
}
