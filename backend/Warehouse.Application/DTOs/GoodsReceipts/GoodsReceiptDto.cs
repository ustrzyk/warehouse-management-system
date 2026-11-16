using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.GoodsReceipts;

public record GoodsReceiptDto(
    int Id,
    string Number,
    int WarehouseId,
    string WarehouseCode,
    string? SupplierName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ReceivedAtUtc
);
