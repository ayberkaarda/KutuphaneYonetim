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
    public LoanInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        // Identity values start at 1, so anything else cannot match a stored record.
        RuleFor(l => l.BookId).GreaterThan(0).WithMessage(Messages.BookNotFound);
        RuleFor(l => l.MemberId).GreaterThan(0).WithMessage(Messages.MemberNotFound);
    }
}
