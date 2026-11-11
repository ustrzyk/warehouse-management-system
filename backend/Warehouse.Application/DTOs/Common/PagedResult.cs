using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.Common;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
