
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class ProductBatchConfiguration
    : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        builder.ToTable("ProductBatches");

        builder.HasKey(x => x.Id);

        builder.HasAlternateKey(x => new
        {
            x.Id,
            x.ProductId
        });

        builder.Property(x => x.BatchNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ExpirationDate)
            .HasColumnType("date");

        builder.Property(x => x.ProductionDate)
            .HasColumnType("date");

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => new
        {
            x.ProductId,
            x.BatchNumber
        })
            .IsUnique();

        builder.HasOne(x => x.Product)
            .WithMany(x => x.Batches)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
