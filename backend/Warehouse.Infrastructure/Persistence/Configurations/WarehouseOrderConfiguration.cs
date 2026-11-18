
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class WarehouseOrderConfiguration
    : IEntityTypeConfiguration<WarehouseOrder>
{
    public void Configure(EntityTypeBuilder<WarehouseOrder> builder)
    {
        builder.ToTable("WarehouseOrders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Number)
            .IsUnique();

        builder.Property(x => x.CustomerName)
            .HasMaxLength(150);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(x => x.Revision)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new
        {
            x.WarehouseId,
            x.Status
        });

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ReleasedAtUtc)
            .HasColumnType("datetime2(3)");
    }
}
