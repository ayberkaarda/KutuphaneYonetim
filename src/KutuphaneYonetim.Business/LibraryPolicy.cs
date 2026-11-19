using KutuphaneYonetim.Business.Validation;

namespace KutuphaneYonetim.Business;

/// <summary>
/// Configurable lending rules, bound from the <c>Library</c> section of the application settings.
/// The defaults apply when the section or a value is missing.
/// </summary>
public sealed class LibraryPolicy
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Library";

    /// <summary>Default loan period, used to suggest the due date when a book is lent.</summary>
    public int LoanPeriodDays { get; set; } = 14;

    /// <summary>Fine charged for every day a book is returned after its due date.</summary>
    public decimal DailyFine { get; set; } = 2.50m;

    /// <summary>
    /// Largest unpaid fine total with which a member may still borrow. With the default of 0 any unpaid
    /// fine blocks new loans.
    /// </summary>
    public decimal MaxUnpaidFine { get; set; }

    /// <summary>
    /// How long a returned book is held for the member at the front of its reservation queue before the
    /// reservation expires and the next member is served.
    /// </summary>
    public int ReservationHoldDays { get; set; } = 3;

    /// <summary>Returns the (Turkish) problems with the configured values; empty when the policy is valid.</summary>
    public IReadOnlyList<string> Validate() =>
        new LibraryPolicyValidator().Validate(this).Errors.Select(e => e.ErrorMessage).ToList();
}
