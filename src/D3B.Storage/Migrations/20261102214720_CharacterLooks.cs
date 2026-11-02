using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D3B.Storage.Migrations
{
    /// <inheritdoc />
    public partial class CharacterLooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BreedId",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFemale",
                table: "Characters",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreedId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "IsFemale",
                table: "Characters");
        }
    }
}
