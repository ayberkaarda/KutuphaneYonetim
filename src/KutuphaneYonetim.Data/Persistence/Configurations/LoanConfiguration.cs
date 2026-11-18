using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KutuphaneYonetim.Data.Persistence.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("OduncIslemleri", table => table.HasCheckConstraint(
            "CK_OduncIslemleri_iade_tarihi",
            "[iade_tarihi] IS NULL OR [iade_tarihi] >= [odunc_tarihi]"));

        builder.HasKey(l => l.Id).HasName("PK_OduncIslemleri");
        builder.Property(l => l.Id).HasColumnName("odunc_id").ValueGeneratedOnAdd();
        builder.Property(l => l.BookId).HasColumnName("kitap_id");
        builder.Property(l => l.MemberId).HasColumnName("uye_id");
        builder.Property(l => l.LoanDate).HasColumnName("odunc_tarihi").HasColumnType("datetime");
        builder.Property(l => l.ReturnDate).HasColumnName("iade_tarihi").HasColumnType("datetime");
        builder.Property<byte[]>(LibraryDbContext.RowVersion).IsRowVersion();
        builder.Ignore(l => l.IsActive);

        // Loans are history: deleting a book or member that has loans is refused (no cascade).
        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(l => l.BookId)
            .HasConstraintName("FK_OduncIslemleri_Kitaplar")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(l => l.MemberId)
            .HasConstraintName("FK_OduncIslemleri_Uyeler")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.BookId, "IX_OduncIslemleri_kitap_id");
        builder.HasIndex(l => l.MemberId, "IX_OduncIslemleri_uye_id");

        // A book can have at most one open (not yet returned) loan at a time.
        builder.HasIndex(l => l.BookId, "UX_OduncIslemleri_active_kitap")
            .IsUnique()
            .HasFilter("[iade_tarihi] IS NULL");

        builder.HasData(SeedData.Loans);
    }
}
