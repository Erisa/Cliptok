using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cliptok.Migrations
{
    /// <inheritdoc />
    public partial class messagestickers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StickerId",
                table: "Messages",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stickers",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stickers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_StickerId",
                table: "Messages",
                column: "StickerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Stickers_StickerId",
                table: "Messages",
                column: "StickerId",
                principalTable: "Stickers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Stickers_StickerId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "Stickers");

            migrationBuilder.DropIndex(
                name: "IX_Messages_StickerId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "StickerId",
                table: "Messages");
        }
    }
}
