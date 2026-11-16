
using System.ComponentModel.DataAnnotations;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.DTOs.GoodsReceipts;

public class GoodsReceiptListQuery
{
    [Range(1, int.MaxValue)]
    public int? WarehouseId { get; set; }

    [StringLength(100)]
    public string? Search { get; set; }

    public GoodsReceiptStatus? Status { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
