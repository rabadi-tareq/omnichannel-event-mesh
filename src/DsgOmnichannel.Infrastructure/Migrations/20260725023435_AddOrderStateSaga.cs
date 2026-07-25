using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DsgOmnichannel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStateSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "OrderState",
                schema: "dbo",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OrderPlacedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderState", x => x.CorrelationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderState",
                schema: "dbo");
        }
    }
}
