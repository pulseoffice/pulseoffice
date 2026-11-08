using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PulseOffice.Exchange;

public static class ExchangeServiceCollectionExtensions
{
    public static IServiceCollection AddObjectExchange(this IServiceCollection services)
    {
        services.TryAddSingleton<IExchangeTypeCatalog, LoadedExchangeTypeCatalog>();
        services.TryAddSingleton<IExchangeContentGuard, ExchangeContentGuard>();
        services.TryAddSingleton<IExchangeSerializer, JsonExchangeSerializer>();
        services.TryAddSingleton<ICellGridConversions, CellGridConversions>();
        services.TryAddSingleton<IObjectExchange, ObjectExchange>();
        return services;
    }
}
