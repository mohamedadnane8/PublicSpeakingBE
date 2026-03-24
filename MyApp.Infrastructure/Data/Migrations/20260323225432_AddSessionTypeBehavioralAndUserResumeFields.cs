using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTypeBehavioralAndUserResumeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetectedField",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeLanguage",
                table: "Users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeUploadHistory",
                table: "Users",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "InterviewQuestions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuestions_Category",
                table: "InterviewQuestions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuestions_Language",
                table: "InterviewQuestions",
                column: "Language");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InterviewQuestions_Category",
                table: "InterviewQuestions");

            migrationBuilder.DropIndex(
                name: "IX_InterviewQuestions_Language",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "DetectedField",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResumeLanguage",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResumeUploadHistory",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "InterviewQuestions");
        }
    }
}
