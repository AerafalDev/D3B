using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D3B.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Npcs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapNpcs",
                columns: table => new
                {
                    MapId = table.Column<long>(type: "INTEGER", nullable: false),
                    NpcId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cell = table.Column<int>(type: "INTEGER", nullable: true),
                    Direction = table.Column<int>(type: "INTEGER", nullable: true),
                    Seen = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapNpcs", x => new { x.MapId, x.NpcId });
                });

            migrationBuilder.CreateTable(
                name: "NpcOutcomes",
                columns: table => new
                {
                    MapId = table.Column<long>(type: "INTEGER", nullable: false),
                    NpcId = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Target = table.Column<long>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcOutcomes", x => new { x.MapId, x.NpcId, x.MessageId, x.ReplyId, x.Kind, x.Target });
                });

            migrationBuilder.CreateTable(
                name: "NpcQuestions",
                columns: table => new
                {
                    MapId = table.Column<long>(type: "INTEGER", nullable: false),
                    NpcId = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    Replies = table.Column<string>(type: "TEXT", nullable: false),
                    Opened = table.Column<int>(type: "INTEGER", nullable: false),
                    Seen = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcQuestions", x => new { x.MapId, x.NpcId, x.MessageId, x.Replies });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapNpcs");

            migrationBuilder.DropTable(
                name: "NpcOutcomes");

            migrationBuilder.DropTable(
                name: "NpcQuestions");
        }
    }
}
