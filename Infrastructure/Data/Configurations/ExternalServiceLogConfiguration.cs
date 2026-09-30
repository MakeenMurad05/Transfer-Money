using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class ExternalServiceLogConfiguration : IEntityTypeConfiguration<ExternalServiceLog>
{
    public void Configure(EntityTypeBuilder<ExternalServiceLog> builder)
    {
        builder.ToTable("ExternalServiceLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ServiceName).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Request).IsRequired();
    }
}