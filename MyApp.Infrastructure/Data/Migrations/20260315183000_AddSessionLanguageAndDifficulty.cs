using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Infrastructure.Data.Migrations;

public partial class AddSessionLanguageAndDifficulty : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Difficulty",
            table: "Sessions",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Medium");

        migrationBuilder.AddColumn<string>(
            name: "Language",
            table: "Sessions",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            defaultValue: "En");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_Difficulty",
            table: "Sessions",
            column: "Difficulty");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_Language",
            table: "Sessions",
            column: "Language");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Sessions_Difficulty",
            table: "Sessions");

        migrationBuilder.DropIndex(
            name: "IX_Sessions_Language",
            table: "Sessions");

        migrationBuilder.DropColumn(
            name: "Difficulty",
            table: "Sessions");

        migrationBuilder.DropColumn(
            name: "Language",
            table: "Sessions");
    }
}
