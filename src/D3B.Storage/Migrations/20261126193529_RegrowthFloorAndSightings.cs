using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D3B.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RegrowthFloorAndSightings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AtLeastMilliseconds",
                table: "Regrowths",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sightings",
                columns: table => new
                {
                    MapId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Elements = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sightings", x => x.MapId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sightings");

            migrationBuilder.DropColumn(
                name: "AtLeastMilliseconds",
                table: "Regrowths");
        }
    }
}
