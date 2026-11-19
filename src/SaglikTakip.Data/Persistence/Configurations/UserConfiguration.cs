using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Data.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Kullanicilar", table =>
        {
            table.HasCheckConstraint("CK_Kullanicilar_Yas", SchemaLimits.AgeCheck);
            table.HasCheckConstraint("CK_Kullanicilar_Cinsiyet", SchemaLimits.GenderCheck);
        });

        builder.HasKey(u => u.Id).HasName("PK_Kullanicilar");
        builder.Property(u => u.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(u => u.Name).HasColumnName("Ad").HasMaxLength(SchemaLimits.UserNameLength).IsRequired();
        builder.Property(u => u.Age).HasColumnName("Yas");
        builder.Property(u => u.Gender).HasColumnName("Cinsiyet").HasMaxLength(SchemaLimits.GenderLength).IsRequired();
        builder.Property<byte[]>(HealthDbContext.RowVersion).IsRowVersion();
        builder.Ignore(u => u.DisplayName);

        builder.HasIndex(u => u.Name, "IX_Kullanicilar_Ad");

        builder.HasData(SeedData.Users);
    }
}
