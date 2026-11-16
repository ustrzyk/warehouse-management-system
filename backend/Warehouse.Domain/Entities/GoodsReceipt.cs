
using Warehouse.Domain.Enums;

namespace Warehouse.Domain.Entities;

public class GoodsReceipt
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public string? SupplierName { get; set; }

    public GoodsReceiptStatus Status { get; set; }
        = GoodsReceiptStatus.Draft;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ReceivedAtUtc { get; set; }

    public ICollection<GoodsReceiptItem> Items { get; set; }
        = new List<GoodsReceiptItem>();
}
