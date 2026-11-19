using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KutuphaneYonetim.Data.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    /// <summary>Filter matching the active statuses (Waiting = 0, Ready = 1).</summary>
    public const string ActiveFilter = "[durum] IN (0, 1)";

    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Rezervasyonlar", table =>
        {
            table.HasCheckConstraint("CK_Rezervasyonlar_durum", "[durum] BETWEEN 0 AND 4");
            table.HasCheckConstraint(
                "CK_Rezervasyonlar_hazir",
                "[durum] <> 1 OR ([hazir_tarihi] IS NOT NULL AND [son_tarih] IS NOT NULL)");
        });

        builder.HasKey(r => r.Id).HasName("PK_Rezervasyonlar");
        builder.Property(r => r.Id).HasColumnName("rezervasyon_id").ValueGeneratedOnAdd();
        builder.Property(r => r.BookId).HasColumnName("kitap_id");
        builder.Property(r => r.MemberId).HasColumnName("uye_id");
        builder.Property(r => r.CreatedAt).HasColumnName("olusturma_tarihi").HasColumnType("datetime");
        builder.Property(r => r.Status).HasColumnName("durum").HasConversion<int>();
        builder.Property(r => r.ReadyAt).HasColumnName("hazir_tarihi").HasColumnType("datetime");
        builder.Property(r => r.ExpiresAt).HasColumnName("son_tarih").HasColumnType("datetime");
        builder.Property(r => r.ClosedAt).HasColumnName("kapanis_tarihi").HasColumnType("datetime");
        builder.Property<byte[]>(LibraryDbContext.RowVersion).IsRowVersion();
        builder.Ignore(r => r.IsActive);

        // Reservations are history like loans: books and members that have them are not deleted.
        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(r => r.BookId)
            .HasConstraintName("FK_Rezervasyonlar_Kitaplar")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(r => r.MemberId)
            .HasConstraintName("FK_Rezervasyonlar_Uyeler")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.MemberId, "IX_Rezervasyonlar_uye_id");
        builder.HasIndex(r => new { r.BookId, r.Status, r.CreatedAt }, "IX_Rezervasyonlar_kitap_durum");

        // A member has at most one active reservation per book.
        builder.HasIndex(r => new { r.BookId, r.MemberId }, "UX_Rezervasyonlar_active_kitap_uye")
            .IsUnique()
            .HasFilter(ActiveFilter);

        // A book is held for at most one member at a time.
        builder.HasIndex(r => r.BookId, "UX_Rezervasyonlar_hold_kitap")
            .IsUnique()
            .HasFilter("[durum] = 1");
    }
}
