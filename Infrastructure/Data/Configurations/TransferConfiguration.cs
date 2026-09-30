using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Reference).IsRequired().HasMaxLength(50);
        builder.HasIndex(t => t.Reference).IsUnique();

        builder.Property(t => t.AccountNumber).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Amount).HasPrecision(18, 3);
        builder.Property(t => t.Currency).IsRequired().HasMaxLength(3);

        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(t => t.BankReference).HasMaxLength(100);
        builder.Property(t => t.ResponseCode).HasMaxLength(20);
        builder.Property(t => t.ResponseMessage).HasMaxLength(500);
    }
}