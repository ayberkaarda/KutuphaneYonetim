using KutuphaneYonetim.Business.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KutuphaneYonetim.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>Registers the business services. Repositories must be registered separately.</summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IBookService, BookService>();
        services.AddSingleton<IMemberService, MemberService>();
        services.AddSingleton<ILoanService, LoanService>();
        return services;
    }
}
