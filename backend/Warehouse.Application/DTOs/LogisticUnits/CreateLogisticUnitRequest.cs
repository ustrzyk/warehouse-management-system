
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.LogisticUnits;

public class CreateLogisticUnitRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;
}
