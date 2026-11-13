
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.LogisticUnits;

public class LogisticUnitListQuery
{
    [StringLength(100)]
    public string? Search { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
