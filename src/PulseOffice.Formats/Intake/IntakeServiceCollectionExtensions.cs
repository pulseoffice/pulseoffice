using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PulseOffice.Formats.Intake;

public static class IntakeServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentIntake(this IServiceCollection services)
    {
        services.TryAddSingleton<IncomingDocumentLimits>();
        services.TryAddSingleton<IIncomingDocumentInspector, IncomingDocumentInspector>();
        services.TryAddSingleton<IDocumentIntake, DocumentIntake>();
        services.TryAddSingleton<IDocumentBuffer, DocumentBuffer>();
        services.TryAddSingleton<PulseOffice.Formats.ITextDocumentInterchange, PulseOffice.Formats.TextDocumentInterchange>();
        return services;
    }
}
