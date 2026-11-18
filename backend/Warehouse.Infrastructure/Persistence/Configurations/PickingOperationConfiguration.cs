
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class PickingOperationConfiguration
    : IEntityTypeConfiguration<PickingOperation>
{
    public void Configure(
        EntityTypeBuilder<PickingOperation> builder)
    {
        builder.ToTable("PickingOperations", table =>
        {
            table.HasCheckConstraint(
                "CK_PickingOperations_Quantity_Positive",
                "[Quantity] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestId)
            .IsRequired();

        builder.HasIndex(x => x.RequestId)
            .IsUnique();

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(x => x.LogisticUnitCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.BatchNumber)
            .HasMaxLength(100);

        builder.Property(x => x.SourceLocationCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PickedAtUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.WarehouseOrderId,
            x.PickedAtUtc
        });

        builder.HasIndex(x => x.WarehouseOrderItemId);
        builder.HasIndex(x => x.LogisticUnitId);
        builder.HasIndex(x => x.LogisticUnitItemId);

        builder.HasOne(x => x.WarehouseOrder)
            .WithMany()
            .HasForeignKey(x => x.WarehouseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WarehouseOrderItem)
            .WithMany()
            .HasForeignKey(x => x.WarehouseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LogisticUnit)
            .WithMany()
            .HasForeignKey(x => x.LogisticUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LogisticUnitItem)
            .WithMany()
            .HasForeignKey(x => x.LogisticUnitItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
