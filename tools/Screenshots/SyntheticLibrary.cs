using System.Globalization;
using KutuphaneYonetim.Business.Reports;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Screenshots;

/// <summary>
/// An invented library for the screenshots: made-up titles, authors and members, and loans,
/// fines and reservations dated relative to today, so every run shows a recent, realistic state.
/// The services answer synchronously from memory; nothing touches SQL Server.
/// </summary>
internal sealed class SyntheticLibrary
{
    private const decimal DailyFine = 2.50m;
    private const int PageSize = 8;

    private readonly DateTime _today = DateTime.Today;
    private readonly List<Book> _books = [];
    private readonly List<Member> _members = [];
    private readonly List<Loan> _loans = [];
    private readonly List<Reservation> _reservations = [];

    public SyntheticLibrary()
    {
        string[][] books =
        [
            ["Sisli Vadinin Bekçisi", "Derin Aksoy"],
            ["Kırk Bir Basamak", "Ozan Temiz"],
            ["Mavi Defterin Sırrı", "Lale Erdem"],
            ["Rüzgârın Unuttuğu Ada", "Selin Karaca"],
            ["Taş Köprü Hikâyeleri", "Emre Tunalı"],
            ["Son Vapur", "Pınar Ilgaz"],
            ["Bakır Saatler", "Kaan Önal"],
            ["Yıldız Haritacısı", "Ece Dalgıç"],
            ["Kumdan Kaleler", "Barış Yener"],
            ["Eski Fenerin Işığı", "Nehir Aydın"],
            ["Unutulmuş Bahçeler", "Cem Arıkan"],
            ["Kuzey Rüzgârı", "Defne Sarı"],
            ["Çınar Altında Bir Yaz", "Tolga Eren"],
            ["Kayıp Mektuplar Arşivi", "Lale Erdem"],
        ];
        for (var i = 0; i < books.Length; i++)
        {
            _books.Add(new Book { Id = i + 1, Title = books[i][0], Author = books[i][1], Isbn = Isbn(i) });
        }

        string[][] members =
        [
            ["Ayla", "Korkmaz"], ["Burak", "Şen"], ["Ceren", "Yalın"], ["Deniz", "Aktaş"],
            ["Efe", "Kılıç"], ["Gizem", "Toprak"], ["Hakan", "Erim"], ["İpek", "Sönmez"],
            ["Kerem", "Uslu"], ["Melis", "Akın"], ["Nazlı", "Çetin"], ["Onur", "Bal"],
        ];
        for (var i = 0; i < members.Length; i++)
        {
            _members.Add(new Member { Id = i + 1, FirstName = members[i][0], LastName = members[i][1] });
        }

        // (book, member, loaned days ago, due in days, returned days ago, fine paid days ago)
        (int Book, int Member, int Loaned, int Due, int? Returned, int? Paid)[] loans =
        [
            (4, 8, 60, -46, 40, 38),
            (1, 4, 45, -31, 33, null),
            (6, 10, 40, -26, 20, null),
            (7, 2, 30, -16, 12, null),
            (3, 5, 25, -11, 9, 9),
            (2, 1, 20, -6, null, null),
            (9, 6, 16, -2, null, null),
            (5, 3, 10, 4, null, null),
            (12, 9, 8, 6, null, null),
            (11, 7, 3, 11, null, null),
            (13, 2, 1, 13, null, null),
        ];
        for (var i = 0; i < loans.Length; i++)
        {
            var (book, member, loaned, due, returned, paid) = loans[i];
            var loan = new Loan
            {
                Id = 101 + i,
                BookId = book,
                MemberId = member,
                LoanDate = _today.AddDays(-loaned).AddHours(10).AddMinutes(17 * (i + 1)),
                DueDate = _today.AddDays(due),
                ReturnDate = returned is { } r ? _today.AddDays(-r).AddHours(15).AddMinutes(5 * i) : null,
            };
            if (loan.ReturnDate is { } back && OverdueDays(loan.DueDate, back) is var late and > 0)
            {
                loan.FineAmount = Fine(late, DailyFine);
                loan.FinePaidDate = paid is { } p ? _today.AddDays(-p).AddHours(16) : null;
            }

            _loans.Add(loan);
        }

        _reservations.AddRange(
        [
            new Reservation { Id = 31, BookId = 2, MemberId = 4, CreatedAt = _today.AddDays(-5).AddHours(11), Status = ReservationStatus.Waiting },
            new Reservation { Id = 32, BookId = 9, MemberId = 12, CreatedAt = _today.AddDays(-3).AddHours(14), Status = ReservationStatus.Waiting },
            new Reservation { Id = 33, BookId = 2, MemberId = 7, CreatedAt = _today.AddDays(-2).AddHours(9), Status = ReservationStatus.Waiting },
            new Reservation
            {
                Id = 34,
                BookId = 8,
                MemberId = 11,
                CreatedAt = _today.AddDays(-9).AddHours(13),
                Status = ReservationStatus.Ready,
                ReadyAt = _today.AddDays(-1).AddHours(10),
                ExpiresAt = _today.AddDays(2).AddHours(10),
            },
        ]);

        Books = new BookService(this);
        Members = new MemberService(this);
        Loans = new LoanService(this);
        Reservations = new ReservationService(this);
        Reports = new ReportService(this);
    }

    public IBookService Books { get; }

    public IMemberService Members { get; }

    public ILoanService Loans { get; }

    public IReservationService Reservations { get; }

    public IReportService Reports { get; }

    public ICsvExporter Csv { get; } = new UnusedCsvExporter();

    private bool IsAvailable(int bookId) =>
        !_loans.Any(l => l.BookId == bookId && l.IsActive)
        && !_reservations.Any(r => r.BookId == bookId && r.Status == ReservationStatus.Ready);

    private Book BookWithState(Book book) => new()
    {
        Id = book.Id,
        Title = book.Title,
        Author = book.Author,
        Isbn = book.Isbn,
        IsAvailable = IsAvailable(book.Id),
    };

    private string MemberName(int id) => _members.Single(m => m.Id == id).FullName;

    private LoanDetail Detail(Loan loan)
    {
        var overdue = OverdueDays(loan.DueDate, loan.ReturnDate ?? _today);
        return new LoanDetail
        {
            Id = loan.Id,
            BookId = loan.BookId,
            BookTitle = _books.Single(b => b.Id == loan.BookId).Title,
            MemberId = loan.MemberId,
            MemberName = MemberName(loan.MemberId),
            LoanDate = loan.LoanDate,
            DueDate = loan.DueDate,
            ReturnDate = loan.ReturnDate,
            FineAmount = loan.FineAmount,
            FinePaidDate = loan.FinePaidDate,
            OverdueDays = overdue,
            EstimatedFine = loan.IsActive && overdue > 0 ? Fine(overdue, DailyFine) : null,
        };
    }

    /// <summary>Whole calendar days past the due date, as the lending rules count them.</summary>
    private static int OverdueDays(DateTime? due, DateTime at) => due is { } d ? Math.Max(0, (at.Date - d.Date).Days) : 0;

    private static decimal Fine(int overdueDays, decimal dailyFine) => overdueDays <= 0 ? 0m : decimal.Round(overdueDays * dailyFine, 2);

    private static PagedResult<T> Page<T>(IReadOnlyList<T> items, int page)
    {
        var count = Math.Max(1, (items.Count + PageSize - 1) / PageSize);
        var current = Math.Clamp(page, 1, count);
        return new PagedResult<T>(items.Skip((current - 1) * PageSize).Take(PageSize).ToList(), items.Count, current, PageSize);
    }

    private static bool Matches(string? text, params string?[] values) =>
        string.IsNullOrWhiteSpace(text)
        || values.Any(v => v is not null && CultureInfo.CurrentCulture.CompareInfo.IndexOf(v, text.Trim(), CompareOptions.IgnoreCase) >= 0);

    /// <summary>A valid ISBN-13 in the 978-605 range with an invented publisher and title number.</summary>
    private static string Isbn(int index)
    {
        var digits = string.Format(CultureInfo.InvariantCulture, "97860541{0:D4}", 1200 + (index * 37));
        var sum = digits.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        var check = (10 - (sum % 10)) % 10;
        return string.Format(CultureInfo.InvariantCulture, "978-605-41-{0}-{1}", digits[8..], check);
    }

    private sealed class BookService(SyntheticLibrary library) : IBookService
    {
        public Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Book>>(library._books.Select(library.BookWithState).ToList());

        public Task<IReadOnlyList<Book>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Book>>(library._books
                .Select(library.BookWithState)
                .Where(b => b.IsAvailable)
                .OrderBy(b => b.Title, StringComparer.CurrentCulture)
                .ToList());

        public Task<PagedResult<Book>> SearchAsync(BookQuery query, CancellationToken cancellationToken = default)
        {
            var books = library._books
                .Select(library.BookWithState)
                .Where(b => Matches(query.Text, b.Title, b.Author, b.Isbn))
                .Where(b => query.Availability switch
                {
                    BookAvailability.Available => b.IsAvailable,
                    BookAvailability.OnLoan => library._loans.Any(l => l.BookId == b.Id && l.IsActive),
                    BookAvailability.Reserved => library._reservations.Any(r => r.BookId == b.Id && r.Status == ReservationStatus.Ready),
                    _ => true,
                })
                .ToList();
            return Task.FromResult(Page(books, query.Page));
        }

        public Task<Book> AddAsync(string title, string author, string? isbn = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not add books.");

        public Task DeleteAsync(int bookId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not delete books.");
    }

    private sealed class MemberService(SyntheticLibrary library) : IMemberService
    {
        public Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Member>>(library._members.OrderBy(m => m.FullName, StringComparer.CurrentCulture).ToList());

        public Task<PagedResult<MemberSummary>> SearchAsync(MemberQuery query, CancellationToken cancellationToken = default)
        {
            var members = library._members
                .Where(m => Matches(query.Text, m.FirstName, m.LastName, m.FullName))
                .Select(m => new MemberSummary
                {
                    Id = m.Id,
                    FirstName = m.FirstName,
                    LastName = m.LastName,
                    ActiveLoanCount = library._loans.Count(l => l.MemberId == m.Id && l.IsActive),
                    UnpaidFine = library._loans.Where(l => l.MemberId == m.Id && l.HasUnpaidFine).Sum(l => l.FineAmount ?? 0),
                })
                .Where(m => query.Filter switch
                {
                    MemberFilter.WithUnpaidFine => m.UnpaidFine > 0,
                    MemberFilter.WithActiveLoan => m.ActiveLoanCount > 0,
                    _ => true,
                })
                .ToList();
            return Task.FromResult(Page(members, query.Page));
        }

        public Task<Member> AddAsync(string firstName, string lastName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not add members.");

        public Task DeleteAsync(int memberId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not delete members.");

        public Task<decimal> PayFinesAsync(int memberId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not take payments.");
    }

    private sealed class LoanService(SyntheticLibrary library) : ILoanService
    {
        public Task<IReadOnlyList<LoanDetail>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LoanDetail>>(library._loans
                .OrderByDescending(l => l.LoanDate)
                .Select(library.Detail)
                .ToList());

        public DateTime GetDefaultDueDate() => library._today.AddDays(14);

        public Task LendAsync(int bookId, int memberId, DateTime? dueDate = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not lend books.");

        /// <summary>Takes the book back today; a held reservation is reported like the real service does.</summary>
        public Task<ReturnReceipt> ReturnAsync(int bookId, CancellationToken cancellationToken = default)
        {
            var loan = library._loans.Single(l => l.BookId == bookId && l.IsActive);
            var now = library._today.AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute);
            loan.ReturnDate = now;
            var overdue = OverdueDays(loan.DueDate, now);
            var fine = Fine(overdue, DailyFine);
            loan.FineAmount = fine > 0 ? fine : null;

            var next = library._reservations
                .Where(r => r.BookId == bookId && r.Status == ReservationStatus.Waiting)
                .OrderBy(r => r.CreatedAt)
                .FirstOrDefault();
            if (next is null)
            {
                return Task.FromResult(new ReturnReceipt(overdue, fine));
            }

            next.Status = ReservationStatus.Ready;
            next.ReadyAt = now;
            next.ExpiresAt = now.AddDays(3);
            return Task.FromResult(new ReturnReceipt(overdue, fine, library.MemberName(next.MemberId), next.ExpiresAt));
        }
    }

    private sealed class ReservationService(SyntheticLibrary library) : IReservationService
    {
        public Task<IReadOnlyList<ReservationDetail>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            var active = library._reservations.Where(r => r.IsActive).OrderBy(r => r.BookId).ThenBy(r => r.CreatedAt).ToList();
            var details = active.Select(r => new ReservationDetail
            {
                Id = r.Id,
                BookId = r.BookId,
                BookTitle = library._books.Single(b => b.Id == r.BookId).Title,
                MemberId = r.MemberId,
                MemberName = library.MemberName(r.MemberId),
                CreatedAt = r.CreatedAt,
                Status = r.Status,
                ReadyAt = r.ReadyAt,
                ExpiresAt = r.ExpiresAt,
                QueuePosition = active.Where(o => o.BookId == r.BookId).OrderBy(o => o.CreatedAt).ToList().IndexOf(r) + 1,
            });
            return Task.FromResult<IReadOnlyList<ReservationDetail>>(details.ToList());
        }

        public Task ReserveAsync(int bookId, int memberId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not reserve books.");

        public Task CancelAsync(int reservationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not cancel reservations.");

        public Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class ReportService(SyntheticLibrary library) : IReportService
    {
        public Task<IReadOnlyList<LoanDetail>> GetOverdueLoansAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LoanDetail>>(library._loans
                .Where(l => l.IsActive)
                .Select(library.Detail)
                .Where(l => l.OverdueDays > 0)
                .OrderByDescending(l => l.OverdueDays)
                .ToList());

        /// <summary>Invented counts over a longer history than the loans listed on the loan screen.</summary>
        public Task<IReadOnlyList<BookLoanCount>> GetMostBorrowedAsync(
            DateTime firstDay, DateTime lastDay, int top = 10, CancellationToken cancellationToken = default)
        {
            int[] counts = [9, 8, 8, 7, 6, 6, 5, 4, 4, 3, 2, 2, 1, 1];
            int[] order = [2, 6, 1, 9, 4, 11, 3, 13, 5, 8, 10, 7, 12, 14];
            var result = order
                .Select((id, i) => (Book: library._books.Single(b => b.Id == id), Count: counts[i]))
                .Take(top)
                .Select(x => new BookLoanCount { BookId = x.Book.Id, Title = x.Book.Title, Author = x.Book.Author, LoanCount = x.Count })
                .ToList();
            return Task.FromResult<IReadOnlyList<BookLoanCount>>(result);
        }

        public Task<IReadOnlyList<LoanDetail>> GetUnpaidFinesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LoanDetail>>(library._loans
                .Where(l => l.HasUnpaidFine)
                .Select(library.Detail)
                .ToList());
    }

    private sealed class UnusedCsvExporter : ICsvExporter
    {
        public Task ExportAsync(ReportTable table, string path, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The screenshots do not write files.");
    }
}
