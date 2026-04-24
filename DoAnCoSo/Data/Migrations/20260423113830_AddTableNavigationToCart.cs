using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnCoSo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTableNavigationToCart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "CartItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_TableId",
                table: "CartItems",
                column: "TableId");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Tables_TableId",
                table: "CartItems",
                column: "TableId",
                principalTable: "Tables",
                principalColumn: "TableId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_Tables_TableId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_TableId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CartItems");
        }
    }
}
