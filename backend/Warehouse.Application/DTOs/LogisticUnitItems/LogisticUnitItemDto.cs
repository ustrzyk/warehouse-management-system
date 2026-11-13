using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.LogisticUnitItems;

public record LogisticUnitItemDto(
    int Id,
    int LogisticUnitId,
    int ProductId,
    string ProductCode,
    string ProductName,
    int? ProductBatchId,
    string? BatchNumber,
    decimal Quantity
);
