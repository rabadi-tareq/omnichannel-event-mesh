using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DsgOmnichannel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStateFaultedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "dbo",
                table: "OrderState",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FaultedAt",
                schema: "dbo",
                table: "OrderState",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "dbo",
                table: "OrderState");

            migrationBuilder.DropColumn(
                name: "FaultedAt",
                schema: "dbo",
                table: "OrderState");
        }
    }
}
