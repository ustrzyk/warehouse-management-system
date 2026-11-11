
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class StorageLocationConfiguration
    : IEntityTypeConfiguration<StorageLocation>
{
    public void Configure(EntityTypeBuilder<StorageLocation> builder)
    {
        builder.ToTable("StorageLocations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Description)
            .HasMaxLength(250);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => new { x.WarehouseZoneId, x.Code })
            .IsUnique();

        builder.HasOne(x => x.WarehouseZone)
            .WithMany(x => x.Locations)
            .HasForeignKey(x => x.WarehouseZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
