using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Word = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ThinkSeconds = table.Column<int>(type: "integer", nullable: false),
                    SpeakSeconds = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
                    RatingOpening = table.Column<int>(type: "integer", nullable: true),
                    RatingStructure = table.Column<int>(type: "integer", nullable: true),
                    RatingEnding = table.Column<int>(type: "integer", nullable: true),
                    RatingConfidence = table.Column<int>(type: "integer", nullable: true),
                    RatingClarity = table.Column<int>(type: "integer", nullable: true),
                    RatingAuthenticity = table.Column<int>(type: "integer", nullable: true),
                    RatingLanguageExpression = table.Column<int>(type: "integer", nullable: true),
                    OverallScore = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AudioAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    AudioDurationMs = table.Column<int>(type: "integer", nullable: true),
                    AudioRecordingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AudioRecordingEndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AudioErrorCode = table.Column<string>(type: "text", nullable: true),
                    Transcript = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CreatedAt",
                table: "Sessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Status",
                table: "Sessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                table: "Sessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
