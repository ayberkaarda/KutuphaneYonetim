using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KutuphaneYonetim.Data.Persistence.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public const int MaxNameLength = 50;

    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Uyeler");

        builder.HasKey(m => m.Id).HasName("PK_Uyeler");
        builder.Property(m => m.Id).HasColumnName("uye_id").ValueGeneratedOnAdd();
        builder.Property(m => m.FirstName).HasColumnName("ad").HasMaxLength(MaxNameLength).IsRequired();
        builder.Property(m => m.LastName).HasColumnName("soyad").HasMaxLength(MaxNameLength).IsRequired();
        builder.Property<byte[]>(LibraryDbContext.RowVersion).IsRowVersion();
        builder.Ignore(m => m.FullName);

        builder.HasIndex(m => new { m.LastName, m.FirstName }, "IX_Uyeler_soyad_ad");

        builder.HasData(SeedData.Members);
    }
}
