using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPickingOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LogisticUnitItems_Quantity_Positive",
                table: "LogisticUnitItems");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "LogisticUnits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LogisticUnitItems",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "PickingOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseOrderId = table.Column<int>(type: "int", nullable: false),
                    WarehouseOrderItemId = table.Column<int>(type: "int", nullable: false),
                    LogisticUnitId = table.Column<int>(type: "int", nullable: false),
                    LogisticUnitItemId = table.Column<int>(type: "int", nullable: false),
                    LogisticUnitCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceLocationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PickedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickingOperations", x => x.Id);
                    table.CheckConstraint("CK_PickingOperations_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_PickingOperations_LogisticUnitItems_LogisticUnitItemId",
                        column: x => x.LogisticUnitItemId,
                        principalTable: "LogisticUnitItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickingOperations_LogisticUnits_LogisticUnitId",
                        column: x => x.LogisticUnitId,
                        principalTable: "LogisticUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickingOperations_WarehouseOrderItems_WarehouseOrderItemId",
                        column: x => x.WarehouseOrderItemId,
                        principalTable: "WarehouseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickingOperations_WarehouseOrders_WarehouseOrderId",
                        column: x => x.WarehouseOrderId,
                        principalTable: "WarehouseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LogisticUnitItems_Quantity_NonNegative",
                table: "LogisticUnitItems",
                sql: "[Quantity] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PickingOperations_LogisticUnitId",
                table: "PickingOperations",
                column: "LogisticUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PickingOperations_LogisticUnitItemId",
                table: "PickingOperations",
                column: "LogisticUnitItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PickingOperations_RequestId",
                table: "PickingOperations",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickingOperations_WarehouseOrderId_PickedAtUtc",
                table: "PickingOperations",
                columns: new[] { "WarehouseOrderId", "PickedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PickingOperations_WarehouseOrderItemId",
                table: "PickingOperations",
                column: "WarehouseOrderItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickingOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LogisticUnitItems_Quantity_NonNegative",
                table: "LogisticUnitItems");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "LogisticUnits");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LogisticUnitItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LogisticUnitItems_Quantity_Positive",
                table: "LogisticUnitItems",
                sql: "[Quantity] > 0");
        }
    }
}
