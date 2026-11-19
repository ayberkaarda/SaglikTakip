using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Data.Persistence.Configurations;

internal sealed class HealthRecordConfiguration : IEntityTypeConfiguration<HealthRecord>
{
    public void Configure(EntityTypeBuilder<HealthRecord> builder)
    {
        builder.ToTable("SaglikKayitlari", table =>
        {
            table.HasCheckConstraint("CK_SaglikKayitlari_Kilo", SchemaLimits.WeightCheck);
            table.HasCheckConstraint("CK_SaglikKayitlari_Nabiz", SchemaLimits.PulseCheck);
        });

        builder.HasKey(r => r.Id).HasName("PK_SaglikKayitlari");
        builder.Property(r => r.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(r => r.UserId).HasColumnName("KullaniciId");
        builder.Property(r => r.Date).HasColumnName("Tarih").HasColumnType("date");
        builder.Property(r => r.Weight).HasColumnName("Kilo").HasColumnType("float");
        builder.Property(r => r.Pulse).HasColumnName("Nabiz");
        builder.Property(r => r.Notes).HasColumnName("Notlar").HasMaxLength(SchemaLimits.NotesLength);
        builder.Property<byte[]>(HealthDbContext.RowVersion).IsRowVersion();

        // Records are the user's history: deleting a user that has records is refused (no cascade).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .HasConstraintName("FK_SaglikKayitlari_Kullanicilar")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.UserId, r.Date }, "IX_SaglikKayitlari_KullaniciId_Tarih");

        builder.HasData(SeedData.HealthRecords);
    }
}
