using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.ProductCategories;

public record ProductCategoryDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive
);
