
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.LogisticUnits;

public class StoreLogisticUnitRequest
{
    [Range(1, int.MaxValue)]
    public int StorageLocationId { get; set; }
}
