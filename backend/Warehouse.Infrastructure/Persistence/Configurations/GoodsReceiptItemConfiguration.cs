
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class GoodsReceiptItemConfiguration
    : IEntityTypeConfiguration<GoodsReceiptItem>
{
    public void Configure(
        EntityTypeBuilder<GoodsReceiptItem> builder)
    {
        builder.ToTable("GoodsReceiptItems", table =>
        {
            table.HasCheckConstraint(
                "CK_GoodsReceiptItems_Quantity_Positive",
                "[Quantity] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.GoodsReceiptId,
            x.ProductId,
            x.ProductBatchId
        })
            .IsUnique()
            .HasFilter(null);

        builder.HasOne(x => x.GoodsReceipt)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.GoodsReceiptId)
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
