
using Warehouse.Domain.Enums;

namespace Warehouse.Domain.Entities;

public class LogisticUnit
{
    public int Id { get; set; }

    // Numer LE / kod kreskowy palety
    public string Code { get; set; } = string.Empty;

    // Aktualna lokalizacja (opcjonalna)
    public int? StorageLocationId { get; set; }

    public StorageLocation? StorageLocation { get; set; }

    // Status logistyczny
    public LogisticUnitStatus Status { get; set; }
        = LogisticUnitStatus.Created;

    // Data utworzenia w UTC
    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    // Zawartosc palety
    public ICollection<LogisticUnitItem> Items { get; set; }
        = new List<LogisticUnitItem>();
}
