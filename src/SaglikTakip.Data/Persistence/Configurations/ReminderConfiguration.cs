using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Data.Persistence.Configurations;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Hatirlaticilar", table =>
        {
            table.HasCheckConstraint("CK_Hatirlaticilar_Tur", SchemaLimits.ReminderKindCheck);
            table.HasCheckConstraint("CK_Hatirlaticilar_AralikSaat", SchemaLimits.ReminderIntervalCheck);
        });

        builder.HasKey(r => r.Id).HasName("PK_Hatirlaticilar");
        builder.Property(r => r.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(r => r.UserId).HasColumnName("KullaniciId");
        builder.Property(r => r.Kind).HasColumnName("Tur").HasConversion<int>();
        builder.Property(r => r.Title).HasColumnName("Baslik").HasMaxLength(SchemaLimits.ReminderTitleLength).IsRequired();
        builder.Property(r => r.IntervalHours).HasColumnName("AralikSaat");
        builder.Property(r => r.NextDueAt).HasColumnName("SonrakiZaman").HasColumnType("datetime2(0)");
        builder.Property<byte[]>(HealthDbContext.RowVersion).IsRowVersion();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .HasConstraintName("FK_Hatirlaticilar_Kullanicilar")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.UserId, "IX_Hatirlaticilar_KullaniciId");

        // The due check runs every minute while the application is open.
        builder.HasIndex(r => r.NextDueAt, "IX_Hatirlaticilar_SonrakiZaman");
    }
}
