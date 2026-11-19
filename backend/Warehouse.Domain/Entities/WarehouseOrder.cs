
using Warehouse.Domain.Enums;

namespace Warehouse.Domain.Entities;

public class WarehouseOrder
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public string? CustomerName { get; set; }

    public WarehouseOrderStatus Status { get; set; }
        = WarehouseOrderStatus.Draft;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ReleasedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }


    // Zmieniany przy modyfikacji zamowienia.
    public int Revision { get; set; }

    // Kontrola wspolbieznosci SQL Server.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<WarehouseOrderItem> Items { get; set; }
        = new List<WarehouseOrderItem>();
}
