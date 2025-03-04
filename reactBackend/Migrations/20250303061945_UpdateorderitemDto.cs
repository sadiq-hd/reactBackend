using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace reactBackend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateorderitemDto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountId",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_DiscountId",
                table: "OrderItems",
                column: "DiscountId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Discounts_DiscountId",
                table: "OrderItems",
                column: "DiscountId",
                principalTable: "Discounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Discounts_DiscountId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_DiscountId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "OrderItems");
        }
    }
}
