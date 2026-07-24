using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DsgOmnichannel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDomainSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SKU",
                table: "StoreInventories",
                newName: "ProductId");

            migrationBuilder.RenameColumn(
                name: "AvailableQuantity",
                table: "StoreInventories",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "SKU",
                table: "Orders",
                newName: "ProductId");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Orders",
                newName: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "StoreInventories",
                newName: "AvailableQuantity");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "StoreInventories",
                newName: "SKU");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "Orders",
                newName: "SKU");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Orders",
                newName: "CreatedAtUtc");
        }
    }
}
