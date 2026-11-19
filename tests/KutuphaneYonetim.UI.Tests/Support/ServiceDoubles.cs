using KutuphaneYonetim.Business.Reports;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI.Tests.Support;

/// <summary>Prompts that never show a dialog: every question is answered yes and recorded.</summary>
internal sealed class RecordedPrompts
{
    public List<string> Questions { get; } = [];

    public List<string> Warnings { get; } = [];

    public UserPrompts Prompts => new()
    {
        Confirm = text =>
        {
            Questions.Add(text);
            return true;
        },
        Inform = _ => { },
        Warn = Warnings.Add,
    };
}

/// <summary>Book service whose searches are answered by the test, in any order, and whose writes never complete.</summary>
internal sealed class ControlledBookService : IBookService
{
    private readonly Dictionary<string, TaskCompletionSource<PagedResult<Book>>> _searches = [];

    public int AddCalls { get; private set; }

    /// <summary>Answer of <see cref="GetAllAsync"/> and, unless <see cref="ControlAvailable"/> is set, of <see cref="GetAvailableAsync"/>.</summary>
    public List<Book> Books { get; } = [];

    /// <summary>When set, <see cref="GetAvailableAsync"/> waits for <see cref="RespondAvailable"/>.</summary>
    public bool ControlAvailable { get; init; }

    public List<TaskCompletionSource<IReadOnlyList<Book>>> AvailableRequests { get; } = [];

    public Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Book>>([.. Books]);

    public Task<IReadOnlyList<Book>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!ControlAvailable)
        {
            return Task.FromResult<IReadOnlyList<Book>>([.. Books]);
        }

        var request = new TaskCompletionSource<IReadOnlyList<Book>>();
        AvailableRequests.Add(request);
        return request.Task;
    }

    /// <summary>Answers the <paramref name="index"/>-th available-books request.</summary>
    public IReadOnlyList<Book> RespondAvailable(int index, params Book[] books)
    {
        AvailableRequests[index].SetResult(books);
        return books;
    }

    public Task<PagedResult<Book>> SearchAsync(BookQuery query, CancellationToken cancellationToken = default)
    {
        var request = new TaskCompletionSource<PagedResult<Book>>();
        _searches[query.Text ?? string.Empty] = request;
        return request.Task;
    }

    public Task<Book> AddAsync(string title, string author, string? isbn = null, CancellationToken cancellationToken = default)
    {
        AddCalls++;
        return new TaskCompletionSource<Book>().Task;
    }

    public Task DeleteAsync(int bookId, CancellationToken cancellationToken = default) => new TaskCompletionSource().Task;

    public PagedResult<Book> Respond(string text, params Book[] books)
    {
        var result = new PagedResult<Book>(books, books.Length, 1, BookQuery.DefaultPageSize);
        _searches[text].SetResult(result);
        return result;
    }
}

/// <summary>
/// Member service whose searches are answered by the test (or at once from <see cref="Members"/> when
/// <see cref="AnswerImmediately"/> is set) and whose writes never complete.
/// </summary>
internal sealed class ControlledMemberService : IMemberService
{
    private readonly Dictionary<string, TaskCompletionSource<PagedResult<MemberSummary>>> _searches = [];

    public bool AnswerImmediately { get; init; }

    public List<MemberSummary> Members { get; } = [];

    public List<int> Paid { get; } = [];

    public List<int> Deleted { get; } = [];

    /// <summary>Answer of <see cref="GetAllAsync"/>.</summary>
    public List<Member> All { get; } = [];

    public Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Member>>([.. All]);

    public Task<PagedResult<MemberSummary>> SearchAsync(MemberQuery query, CancellationToken cancellationToken = default)
    {
        if (AnswerImmediately)
        {
            // Two members per page, filtered by first name.
            var matching = Members.Where(m => query.Text is null || m.FirstName.Contains(query.Text, StringComparison.Ordinal)).ToList();
            var page = matching.Skip((query.Page - 1) * 2).Take(2).ToList();
            return Task.FromResult(new PagedResult<MemberSummary>(page, matching.Count, query.Page, 2));
        }

        var request = new TaskCompletionSource<PagedResult<MemberSummary>>();
        _searches[query.Text ?? string.Empty] = request;
        return request.Task;
    }

    public Task<Member> AddAsync(string firstName, string lastName, CancellationToken cancellationToken = default) =>
        new TaskCompletionSource<Member>().Task;

    public Task DeleteAsync(int memberId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(memberId);
        return new TaskCompletionSource().Task;
    }

    public Task<decimal> PayFinesAsync(int memberId, CancellationToken cancellationToken = default)
    {
        Paid.Add(memberId);
        return new TaskCompletionSource<decimal>().Task;
    }

    public PagedResult<MemberSummary> Respond(string text, params MemberSummary[] members)
    {
        var result = new PagedResult<MemberSummary>(members, members.Length, 1, BookQuery.DefaultPageSize);
        _searches[text].SetResult(result);
        return result;
    }
}

/// <summary>Loan service whose writes never complete.</summary>
internal sealed class PendingLoanService : ILoanService
{
    public int LendCalls { get; private set; }

    public int ReturnCalls { get; private set; }

    public Task<IReadOnlyList<LoanDetail>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LoanDetail>>([]);

    public DateTime GetDefaultDueDate() => DateTime.Today.AddDays(14);

    public Task LendAsync(int bookId, int memberId, DateTime? dueDate = null, CancellationToken cancellationToken = default)
    {
        LendCalls++;
        return new TaskCompletionSource().Task;
    }

    public Task<ReturnReceipt> ReturnAsync(int bookId, CancellationToken cancellationToken = default)
    {
        ReturnCalls++;
        return new TaskCompletionSource<ReturnReceipt>().Task;
    }
}

/// <summary>Loan service whose list requests are answered by the test, in any order, and whose loans succeed at once.</summary>
internal sealed class ControlledLoanService : ILoanService
{
    public List<TaskCompletionSource<IReadOnlyList<LoanDetail>>> ListRequests { get; } = [];

    public List<int> Lent { get; } = [];

    public Task<IReadOnlyList<LoanDetail>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var request = new TaskCompletionSource<IReadOnlyList<LoanDetail>>();
        ListRequests.Add(request);
        return request.Task;
    }

    public DateTime GetDefaultDueDate() => DateTime.Today.AddDays(14);

    public Task LendAsync(int bookId, int memberId, DateTime? dueDate = null, CancellationToken cancellationToken = default)
    {
        Lent.Add(bookId);
        return Task.CompletedTask;
    }

    public Task<ReturnReceipt> ReturnAsync(int bookId, CancellationToken cancellationToken = default) =>
        new TaskCompletionSource<ReturnReceipt>().Task;

    /// <summary>Answers the <paramref name="index"/>-th list request.</summary>
    public IReadOnlyList<LoanDetail> RespondList(int index, params LoanDetail[] loans)
    {
        ListRequests[index].SetResult(loans);
        return loans;
    }
}

/// <summary>Reservation service whose list requests are answered by the test, in any order, and whose reservations succeed at once.</summary>
internal sealed class ControlledReservationService : IReservationService
{
    public List<TaskCompletionSource<IReadOnlyList<ReservationDetail>>> ListRequests { get; } = [];

    public List<int> Reserved { get; } = [];

    public Task<IReadOnlyList<ReservationDetail>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var request = new TaskCompletionSource<IReadOnlyList<ReservationDetail>>();
        ListRequests.Add(request);
        return request.Task;
    }

    public Task ReserveAsync(int bookId, int memberId, CancellationToken cancellationToken = default)
    {
        Reserved.Add(bookId);
        return Task.CompletedTask;
    }

    public Task CancelAsync(int reservationId, CancellationToken cancellationToken = default) => new TaskCompletionSource().Task;

    public Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    /// <summary>Answers the <paramref name="index"/>-th list request.</summary>
    public IReadOnlyList<ReservationDetail> RespondList(int index, params ReservationDetail[] reservations)
    {
        ListRequests[index].SetResult(reservations);
        return reservations;
    }
}

/// <summary>Reservation service whose writes never complete.</summary>
internal sealed class PendingReservationService : IReservationService
{
    public int ReserveCalls { get; private set; }

    public int CancelCalls { get; private set; }

    public Task<IReadOnlyList<ReservationDetail>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ReservationDetail>>([]);

    public Task ReserveAsync(int bookId, int memberId, CancellationToken cancellationToken = default)
    {
        ReserveCalls++;
        return new TaskCompletionSource().Task;
    }

    public Task CancelAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        CancelCalls++;
        return new TaskCompletionSource().Task;
    }

    public Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

/// <summary>Report service whose answers are completed by the test, in any order.</summary>
internal sealed class ControlledReportService : IReportService
{
    private readonly TaskCompletionSource<IReadOnlyList<LoanDetail>> _overdue = new();
    private readonly TaskCompletionSource<IReadOnlyList<LoanDetail>> _unpaid = new();

    public Task<IReadOnlyList<LoanDetail>> GetOverdueLoansAsync(CancellationToken cancellationToken = default) => _overdue.Task;

    public Task<IReadOnlyList<BookLoanCount>> GetMostBorrowedAsync(
        DateTime firstDay, DateTime lastDay, int top = 10, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookLoanCount>>([]);

    public Task<IReadOnlyList<LoanDetail>> GetUnpaidFinesAsync(CancellationToken cancellationToken = default) => _unpaid.Task;

    public void RespondOverdue(params LoanDetail[] loans) => _overdue.SetResult(loans);

    public void RespondUnpaid(params LoanDetail[] loans) => _unpaid.SetResult(loans);
}
