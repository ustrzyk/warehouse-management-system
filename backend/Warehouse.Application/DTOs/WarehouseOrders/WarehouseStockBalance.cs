using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.WarehouseOrders;

public record WarehouseStockBalance(
    int ProductId,
    int? ProductBatchId,
    decimal Quantity
);
