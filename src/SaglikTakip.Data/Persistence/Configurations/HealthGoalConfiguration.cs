using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Data.Persistence.Configurations;

internal sealed class HealthGoalConfiguration : IEntityTypeConfiguration<HealthGoal>
{
    public void Configure(EntityTypeBuilder<HealthGoal> builder)
    {
        builder.ToTable("Hedefler", table =>
        {
            table.HasCheckConstraint("CK_Hedefler_Kilo", SchemaLimits.GoalWeightCheck);
            table.HasCheckConstraint("CK_Hedefler_KiloAraligi", SchemaLimits.GoalWeightRangeCheck);
            table.HasCheckConstraint("CK_Hedefler_Nabiz", SchemaLimits.GoalPulseCheck);
            table.HasCheckConstraint("CK_Hedefler_NabizAraligi", SchemaLimits.GoalPulseRangeCheck);
        });

        builder.HasKey(g => g.Id).HasName("PK_Hedefler");
        builder.Property(g => g.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(g => g.UserId).HasColumnName("KullaniciId");
        builder.Property(g => g.TargetWeight).HasColumnName("HedefKilo").HasColumnType("float");
        builder.Property(g => g.StartWeight).HasColumnName("BaslangicKilo").HasColumnType("float");
        builder.Property(g => g.MinWeight).HasColumnName("MinKilo").HasColumnType("float");
        builder.Property(g => g.MaxWeight).HasColumnName("MaxKilo").HasColumnType("float");
        builder.Property(g => g.MinPulse).HasColumnName("MinNabiz");
        builder.Property(g => g.MaxPulse).HasColumnName("MaxNabiz");
        builder.Property<byte[]>(HealthDbContext.RowVersion).IsRowVersion();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .HasConstraintName("FK_Hedefler_Kullanicilar")
            .OnDelete(DeleteBehavior.Restrict);

        // One goal per user.
        builder.HasIndex(g => g.UserId, "IX_Hedefler_KullaniciId").IsUnique();
    }
}
