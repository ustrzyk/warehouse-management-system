
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Number)
            .IsUnique();

        // Jedna wysylka na jedno zamowienie.
        builder.HasIndex(x => x.WarehouseOrderId)
            .IsUnique();

        builder.Property(x => x.CarrierName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.TrackingNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(x => x.ShippedAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(x => x.Revision)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.WarehouseOrder)
            .WithMany()
            .HasForeignKey(x => x.WarehouseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
