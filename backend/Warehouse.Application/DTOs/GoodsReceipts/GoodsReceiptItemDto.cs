using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.GoodsReceipts;

public record GoodsReceiptItemDto(
    int Id,
    int GoodsReceiptId,
    int ProductId,
    string ProductCode,
    string ProductName,
    int? ProductBatchId,
    string? BatchNumber,
    decimal Quantity
);
