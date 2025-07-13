using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cliptok.Migrations
{
    /// <inheritdoc />
    public partial class bulkmessagelogstore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BulkMessageLogStore",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PasteUrl = table.Column<string>(type: "text", nullable: true),
                    DiscordUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkMessageLogStore", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BulkMessageLogStoreCachedDiscordUser",
                columns: table => new
                {
                    BulkMessageLogsId = table.Column<int>(type: "integer", nullable: false),
                    UsersId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkMessageLogStoreCachedDiscordUser", x => new { x.BulkMessageLogsId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_BulkMessageLogStoreCachedDiscordUser_BulkMessageLogStore_Bu~",
                        column: x => x.BulkMessageLogsId,
                        principalTable: "BulkMessageLogStore",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BulkMessageLogStoreCachedDiscordUser_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulkMessageLogStoreCachedDiscordUser_UsersId",
                table: "BulkMessageLogStoreCachedDiscordUser",
                column: "UsersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulkMessageLogStoreCachedDiscordUser");

            migrationBuilder.DropTable(
                name: "BulkMessageLogStore");
        }
    }
}
