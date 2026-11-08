using Microsoft.Extensions.DependencyInjection;

namespace PulseOffice.Localization;

public static class LocalizationRegistration
{
    public static IServiceCollection AddLocalization(
    this IServiceCollection services, UiLanguage language = UiLanguage.System)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ITextCatalog>(_ => new TextCatalog(language));
        services.AddSingleton<IDiagnosticTexts, DiagnosticTexts>();
        services.AddSingleton<IInterfaceCulture>(provider => new InterfaceCulture(provider.GetRequiredService<ITextCatalog>()));
        return services;
    }
}
