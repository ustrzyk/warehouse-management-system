
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class WarehouseOrderItemConfiguration
    : IEntityTypeConfiguration<WarehouseOrderItem>
{
    public void Configure(
        EntityTypeBuilder<WarehouseOrderItem> builder)
    {
        builder.ToTable("WarehouseOrderItems", table =>
        {
            table.HasCheckConstraint(
                "CK_WarehouseOrderItems_Quantity_Positive",
                "[QuantityRequested] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuantityRequested)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.WarehouseOrderId,
            x.ProductId,
            x.ProductBatchId
        })
            .IsUnique()
            .HasFilter(null);

        builder.HasOne(x => x.WarehouseOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.WarehouseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductBatch)
            .WithMany()
            .HasForeignKey(x => new
            {
                x.ProductBatchId,
                x.ProductId
            })
            .HasPrincipalKey(x => new
            {
                x.Id,
                x.ProductId
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
