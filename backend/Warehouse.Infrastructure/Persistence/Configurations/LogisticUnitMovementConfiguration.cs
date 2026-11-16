
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warehouse.Domain.Entities;

namespace Warehouse.Infrastructure.Persistence.Configurations;

public class LogisticUnitMovementConfiguration
    : IEntityTypeConfiguration<LogisticUnitMovement>
{
    public void Configure(
        EntityTypeBuilder<LogisticUnitMovement> builder)
    {
        builder.ToTable("LogisticUnitMovements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FromLocationCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ToLocationCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.MovedAtUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(250);

        builder.HasIndex(x => new
        {
            x.LogisticUnitId,
            x.MovedAtUtc
        });

        builder.HasOne(x => x.LogisticUnit)
            .WithMany()
            .HasForeignKey(x => x.LogisticUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FromLocation)
            .WithMany()
            .HasForeignKey(x => x.FromStorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToLocation)
            .WithMany()
            .HasForeignKey(x => x.ToStorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
