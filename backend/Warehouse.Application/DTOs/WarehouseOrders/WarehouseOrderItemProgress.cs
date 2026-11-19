using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.WarehouseOrders;

public record WarehouseOrderItemProgress(
    int WarehouseOrderItemId,
    decimal QuantityRequested,
    decimal QuantityPicked
);
