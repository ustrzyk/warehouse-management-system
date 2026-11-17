using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarehouseOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseOrders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductBatchId = table.Column<int>(type: "int", nullable: true),
                    QuantityRequested = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseOrderItems", x => x.Id);
                    table.CheckConstraint("CK_WarehouseOrderItems_Quantity_Positive", "[QuantityRequested] > 0");
                    table.ForeignKey(
                        name: "FK_WarehouseOrderItems_ProductBatches_ProductBatchId_ProductId",
                        columns: x => new { x.ProductBatchId, x.ProductId },
                        principalTable: "ProductBatches",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseOrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseOrderItems_WarehouseOrders_WarehouseOrderId",
                        column: x => x.WarehouseOrderId,
                        principalTable: "WarehouseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseOrderItems_ProductBatchId_ProductId",
                table: "WarehouseOrderItems",
                columns: new[] { "ProductBatchId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseOrderItems_ProductId",
                table: "WarehouseOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseOrderItems_WarehouseOrderId_ProductId_ProductBatchId",
                table: "WarehouseOrderItems",
                columns: new[] { "WarehouseOrderId", "ProductId", "ProductBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseOrders_Number",
                table: "WarehouseOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseOrders_WarehouseId_Status",
                table: "WarehouseOrders",
                columns: new[] { "WarehouseId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarehouseOrderItems");

            migrationBuilder.DropTable(
                name: "WarehouseOrders");
        }
    }
}
