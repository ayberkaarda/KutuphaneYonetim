using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Data.Persistence;

/// <summary>
/// Sample data applied by the InitialCreate migration through HasData. Values are fixed so the
/// migration is deterministic; the names are fictional.
/// </summary>
internal static class SeedData
{
    public static readonly Book[] Books =
    [
        new() { Id = 1, Title = "1984", Author = "George Orwell", IsAvailable = true },
        new() { Id = 2, Title = "Hayvan Çiftliği", Author = "George Orwell", IsAvailable = true },
        new() { Id = 3, Title = "Simyacı", Author = "Paulo Coelho", IsAvailable = false },
        new() { Id = 4, Title = "Kürk Mantolu Madonna", Author = "Sabahattin Ali", IsAvailable = true },
        new() { Id = 5, Title = "Suç ve Ceza", Author = "Fyodor Dostoyevski", IsAvailable = true },
        new() { Id = 6, Title = "Sefiller", Author = "Victor Hugo", IsAvailable = true },
        new() { Id = 7, Title = "Tutunamayanlar", Author = "Oğuz Atay", IsAvailable = true },
        new() { Id = 8, Title = "İnce Memed", Author = "Yaşar Kemal", IsAvailable = true },
    ];

    public static readonly Member[] Members =
    [
        new() { Id = 1, FirstName = "Örnek", LastName = "Üye" },
        new() { Id = 2, FirstName = "Deneme", LastName = "Kullanıcı" },
        new() { Id = 3, FirstName = "Test", LastName = "Okur" },
    ];

    // One returned loan and one active loan (book 3 is therefore not available).
    public static readonly Loan[] Loans =
    [
        new()
        {
            Id = 1,
            BookId = 1,
            MemberId = 1,
            LoanDate = new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Unspecified),
            ReturnDate = new DateTime(2025, 1, 20, 16, 30, 0, DateTimeKind.Unspecified),
        },
        new()
        {
            Id = 2,
            BookId = 3,
            MemberId = 2,
            LoanDate = new DateTime(2025, 2, 3, 11, 15, 0, DateTimeKind.Unspecified),
            ReturnDate = null,
        },
    ];
}
