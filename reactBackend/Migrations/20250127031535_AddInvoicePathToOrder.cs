using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace reactBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePathToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoicePath",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoicePath",
                table: "Orders");
        }
    }
}
