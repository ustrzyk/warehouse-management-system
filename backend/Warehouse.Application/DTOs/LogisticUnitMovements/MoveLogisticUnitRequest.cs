
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.LogisticUnitMovements;

public class MoveLogisticUnitRequest
{
    [Range(1, int.MaxValue)]
    public int ToStorageLocationId { get; set; }

    [StringLength(250)]
    public string? Note { get; set; }
}
