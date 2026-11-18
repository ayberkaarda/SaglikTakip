using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Data.Persistence.Configurations;

internal sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Egzersizler", table =>
        {
            table.HasCheckConstraint("CK_Egzersizler_SureDakika", SchemaLimits.DurationCheck);
            table.HasCheckConstraint("CK_Egzersizler_TekrarSayisi", SchemaLimits.RepetitionsCheck);
        });

        builder.HasKey(e => e.Id).HasName("PK_Egzersizler");
        builder.Property(e => e.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("KullaniciId");
        builder.Property(e => e.Name).HasColumnName("EgzersizAdi").HasMaxLength(SchemaLimits.ExerciseNameLength).IsRequired();
        builder.Property(e => e.DurationMinutes).HasColumnName("SureDakika");
        builder.Property(e => e.Repetitions).HasColumnName("TekrarSayisi");
        builder.Property<byte[]>(HealthDbContext.RowVersion).IsRowVersion();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .HasConstraintName("FK_Egzersizler_Kullanicilar")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.UserId, "IX_Egzersizler_KullaniciId");

        builder.HasData(SeedData.Exercises);
    }
}
