using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceiptConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LogisticUnitId",
                table: "GoodsReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "GoodsReceipts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "GoodsReceipts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_LogisticUnitId",
                table: "GoodsReceipts",
                column: "LogisticUnitId",
                unique: true,
                filter: "[LogisticUnitId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_LogisticUnits_LogisticUnitId",
                table: "GoodsReceipts",
                column: "LogisticUnitId",
                principalTable: "LogisticUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_LogisticUnits_LogisticUnitId",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_LogisticUnitId",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "LogisticUnitId",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "GoodsReceipts");
        }
    }
}
