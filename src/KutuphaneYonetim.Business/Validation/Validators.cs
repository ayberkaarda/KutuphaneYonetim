using System.Globalization;
using FluentValidation;

namespace KutuphaneYonetim.Business.Validation;

/// <summary>User facing (Turkish) validation and lookup messages shared by validators and services.</summary>
internal static class Messages
{
    public const string BookNotFound = "Seçilen kitap bulunamadı.";
    public const string MemberNotFound = "Seçilen üye bulunamadı.";

    public static string Required(string fieldName) =>
        string.Format(CultureInfo.CurrentCulture, "{0} alanı boş bırakılamaz.", fieldName);

    public static string TooLong(string fieldName, int maxLength) =>
        string.Format(CultureInfo.CurrentCulture, "{0} en fazla {1} karakter olabilir.", fieldName, maxLength);
}

internal static class ValidationRuleExtensions
{
    /// <summary>The value must be present (after trimming) and not longer than <paramref name="maxLength"/>.</summary>
    public static IRuleBuilderOptions<T, string> RequiredText<T>(
        this IRuleBuilder<T, string> rule, string fieldName, int maxLength) =>
        rule
            .NotEmpty().WithMessage(Messages.Required(fieldName))
            .MaximumLength(maxLength).WithMessage(Messages.TooLong(fieldName, maxLength));

    /// <summary>
    /// Validates <paramref name="input"/> and throws a <see cref="BusinessRuleException"/> carrying the
    /// first failure message, so callers see one clear message at a time.
    /// </summary>
    public static void ValidateOrThrow<T>(this IValidator<T> validator, T input)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var result = validator.Validate(input);
        if (!result.IsValid)
        {
            throw new BusinessRuleException(result.Errors[0].ErrorMessage);
        }
    }
}

internal sealed class BookInputValidator : AbstractValidator<BookInput>
{
    public const int MaxTitleLength = 100;
    public const int MaxAuthorLength = 100;

    public BookInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(b => b.Title).RequiredText("Kitap adı", MaxTitleLength);
        RuleFor(b => b.Author).RequiredText("Yazar", MaxAuthorLength);
    }
}

internal sealed class MemberInputValidator : AbstractValidator<MemberInput>
{
    public const int MaxNameLength = 50;

    public MemberInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.FirstName).RequiredText("Ad", MaxNameLength);
        RuleFor(m => m.LastName).RequiredText("Soyad", MaxNameLength);
    }
}

internal sealed class LoanInputValidator : AbstractValidator<LoanInput>
{
    public const int MaxLoanDays = 365;

    public LoanInputValidator()
    {
        // No id range checks: legacy rows may carry any id, so LoanService decides existence via GetByIdAsync.
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(l => l.DueDate.Date)
            .GreaterThanOrEqualTo(l => l.LoanDate.Date)
            .WithMessage("Teslim tarihi ödünç tarihinden önce olamaz.")
            .LessThanOrEqualTo(l => l.LoanDate.Date.AddDays(MaxLoanDays))
            .WithMessage(string.Format(CultureInfo.CurrentCulture, "Teslim tarihi en fazla {0} gün sonrası olabilir.", MaxLoanDays));
    }
}

internal sealed class LibraryPolicyValidator : AbstractValidator<LibraryPolicy>
{
    public LibraryPolicyValidator()
    {
        RuleFor(p => p.LoanPeriodDays)
            .InclusiveBetween(1, LoanInputValidator.MaxLoanDays)
            .WithMessage(string.Format(
                CultureInfo.CurrentCulture, "LoanPeriodDays 1 ile {0} arasında olmalıdır.", LoanInputValidator.MaxLoanDays));
        RuleFor(p => p.DailyFine)
            .InclusiveBetween(0m, 10_000m)
            .WithMessage("DailyFine 0 ile 10000 arasında olmalıdır.");
        RuleFor(p => p.MaxUnpaidFine)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("MaxUnpaidFine negatif olamaz.");
    }
}
