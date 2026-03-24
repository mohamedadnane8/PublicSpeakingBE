using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewRatingsAndPassion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RatingAction",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingConciseness",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingDeliveryComposure",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingPassion",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingRelevance",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingResultImpact",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingSituationStakes",
                table: "Sessions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatingAction",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RatingConciseness",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RatingDeliveryComposure",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RatingPassion",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RatingRelevance",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RatingResultImpact",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RatingSituationStakes",
                table: "Sessions");
        }
    }
}
