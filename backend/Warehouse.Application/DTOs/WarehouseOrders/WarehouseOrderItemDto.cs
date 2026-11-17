using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.WarehouseOrders;

public record WarehouseOrderItemDto(
    int Id,
    int WarehouseOrderId,
    int ProductId,
    string ProductCode,
    string ProductName,
    int? ProductBatchId,
    string? BatchNumber,
    decimal QuantityRequested
);
