
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.ProductBatches;

public class ProductBatchListQuery
{
    [StringLength(100)]
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int? ProductId { get; set; }

    public bool? IsActive { get; set; }

    [Required]
    [RegularExpression(
        "(?i)^(batchNumber|productionDate|expirationDate)$")]
    public string SortBy { get; set; } = "batchNumber";

    public bool Descending { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
