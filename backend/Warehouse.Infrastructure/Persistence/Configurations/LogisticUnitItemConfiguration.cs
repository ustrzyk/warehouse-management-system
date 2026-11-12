
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class LogisticUnitItemConfiguration
    : IEntityTypeConfiguration<LogisticUnitItem>
{
    public void Configure(
        EntityTypeBuilder<LogisticUnitItem> builder)
    {
        builder.ToTable("LogisticUnitItems", table =>
        {
            table.HasCheckConstraint(
                "CK_LogisticUnitItems_Quantity_Positive",
                "[Quantity] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.LogisticUnitId,
            x.ProductId,
            x.ProductBatchId
        })
            .IsUnique()
            .HasFilter(null);

        builder.HasOne(x => x.LogisticUnit)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.LogisticUnitId)
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
