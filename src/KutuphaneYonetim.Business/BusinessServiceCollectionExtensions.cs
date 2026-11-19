using FluentValidation;
using KutuphaneYonetim.Business.Reports;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Business.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KutuphaneYonetim.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>
    /// Registers the business services. Repositories must be registered separately.
    /// <paramref name="policy"/> defaults to <see cref="LibraryPolicy"/> with its default values.
    /// </summary>
    /// <exception cref="ArgumentException">The policy has invalid values.</exception>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, LibraryPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        policy ??= new LibraryPolicy();
        var problems = policy.Validate();
        if (problems.Count > 0)
        {
            throw new ArgumentException("Invalid library policy: " + string.Join(" ", problems), nameof(policy));
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(policy);
        services.AddSingleton<IValidator<BookInput>, BookInputValidator>();
        services.AddSingleton<IValidator<MemberInput>, MemberInputValidator>();
        services.AddSingleton<IValidator<LoanInput>, LoanInputValidator>();
        services.AddSingleton<IBookService, BookService>();
        services.AddSingleton<IMemberService, MemberService>();
        services.AddSingleton<ILoanService, LoanService>();
        services.AddSingleton<IReservationService, ReservationService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<ICsvExporter, CsvExporter>();
        return services;
    }
}
