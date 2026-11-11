using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.Products;

public record ProductDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    string? Ean,
    int ProductCategoryId,
    string ProductCategoryCode,
    string ProductCategoryName,
    int UnitOfMeasureId,
    string UnitOfMeasureCode,
    string UnitOfMeasureName,
    string UnitOfMeasureSymbol,
    bool IsActive
);
