using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MyApp.Infrastructure.Data;

#nullable disable

namespace MyApp.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Corrective migration for environments where
    /// 20260315190342_AddSessionLanguageAndDifficulty was applied with empty Up/Down.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260315221000_FixMissingSessionLanguageDifficultyColumns")]
    public partial class FixMissingSessionLanguageDifficultyColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Sessions"
                ADD COLUMN IF NOT EXISTS "Language" character varying(10) NOT NULL DEFAULT 'En';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Sessions"
                ADD COLUMN IF NOT EXISTS "Difficulty" character varying(20) NOT NULL DEFAULT 'Medium';
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Sessions_Language"
                ON "Sessions" ("Language");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Sessions_Difficulty"
                ON "Sessions" ("Difficulty");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Sessions_Language";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Sessions_Difficulty";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Sessions" DROP COLUMN IF EXISTS "Language";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Sessions" DROP COLUMN IF EXISTS "Difficulty";
                """);
        }
    }
}
