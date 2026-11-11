using System;
using System.Collections.Generic;
using System.Text;

namespace Warehouse.Application.DTOs.UnitsOfMeasure;

public record UnitOfMeasureDto(
    int Id,
    string Code,
    string Name,
    string Symbol,
    bool IsActive
);