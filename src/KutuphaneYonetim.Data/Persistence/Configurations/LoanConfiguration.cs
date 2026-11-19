using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KutuphaneYonetim.Data.Persistence.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("OduncIslemleri", table =>
        {
            table.HasCheckConstraint(
                "CK_OduncIslemleri_iade_tarihi",
                "[iade_tarihi] IS NULL OR [iade_tarihi] >= [odunc_tarihi]");
            table.HasCheckConstraint(
                "CK_OduncIslemleri_ceza_tutari",
                "[ceza_tutari] IS NULL OR [ceza_tutari] >= 0");
        });

        builder.HasKey(l => l.Id).HasName("PK_OduncIslemleri");
        builder.Property(l => l.Id).HasColumnName("odunc_id").ValueGeneratedOnAdd();
        builder.Property(l => l.BookId).HasColumnName("kitap_id");
        builder.Property(l => l.MemberId).HasColumnName("uye_id");
        builder.Property(l => l.LoanDate).HasColumnName("odunc_tarihi").HasColumnType("datetime");
        builder.Property(l => l.ReturnDate).HasColumnName("iade_tarihi").HasColumnType("datetime");

        // Added after the original schema: nullable, so existing loans keep their values (no due date, no fine).
        builder.Property(l => l.DueDate).HasColumnName("teslim_tarihi").HasColumnType("date");
        builder.Property(l => l.FineAmount).HasColumnName("ceza_tutari").HasPrecision(10, 2);
        builder.Property(l => l.FinePaidDate).HasColumnName("ceza_odeme_tarihi").HasColumnType("datetime");

        builder.Property<byte[]>(LibraryDbContext.RowVersion).IsRowVersion();
        builder.Ignore(l => l.IsActive);
        builder.Ignore(l => l.HasUnpaidFine);

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
        builder.HasIndex(l => l.DueDate, "IX_OduncIslemleri_teslim_tarihi");

        // A book can have at most one open (not yet returned) loan at a time.
        builder.HasIndex(l => l.BookId, "UX_OduncIslemleri_active_kitap")
            .IsUnique()
            .HasFilter("[iade_tarihi] IS NULL");

        builder.HasData(SeedData.Loans);
    }
}
