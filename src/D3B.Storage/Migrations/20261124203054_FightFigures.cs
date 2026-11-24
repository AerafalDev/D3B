using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D3B.Storage.Migrations
{
    /// <inheritdoc />
    public partial class FightFigures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Deaths",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Experience",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "FightsLost",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FightsWon",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Kamas",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deaths",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "Experience",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "FightsLost",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "FightsWon",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "Kamas",
                table: "Runs");
        }
    }
}
