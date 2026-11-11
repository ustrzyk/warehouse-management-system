using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.ProductBatches;

public record ProductBatchDto(
    int Id,
    int ProductId,
    string ProductCode,
    string ProductName,
    string BatchNumber,
    DateOnly? ProductionDate,
    DateOnly? ExpirationDate,
    bool IsActive
);
