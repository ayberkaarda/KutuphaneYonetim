using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KutuphaneYonetim.Data.Persistence.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public const int MaxTitleLength = 100;
    public const int MaxAuthorLength = 100;
    public const int MaxIsbnLength = 13;

    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Kitaplar");

        builder.HasKey(b => b.Id).HasName("PK_Kitaplar");
        builder.Property(b => b.Id).HasColumnName("kitap_id").ValueGeneratedOnAdd();
        builder.Property(b => b.Title).HasColumnName("ad").HasMaxLength(MaxTitleLength).IsRequired();
        builder.Property(b => b.Author).HasColumnName("yazar").HasMaxLength(MaxAuthorLength).IsRequired();
        builder.Property(b => b.IsAvailable).HasColumnName("durum").IsRequired();

        // Added after the original schema: nullable, existing books simply have no ISBN. Several copies of
        // the same edition share an ISBN, so it is not unique.
        builder.Property(b => b.Isbn).HasColumnName("isbn").HasMaxLength(MaxIsbnLength);

        builder.Property<byte[]>(LibraryDbContext.RowVersion).IsRowVersion();

        builder.HasIndex(b => b.Title, "IX_Kitaplar_ad");
        builder.HasIndex(b => b.Isbn, "IX_Kitaplar_isbn");

        builder.HasData(SeedData.Books);
    }
}
