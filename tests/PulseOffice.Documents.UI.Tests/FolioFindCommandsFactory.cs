using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Tests;

internal sealed class FolioFindCommandsFactory
{
    public FolioFindCommands Create(IServiceProvider services) =>
        new(services.GetRequiredService<IActiveFolioContext>(), services.GetRequiredService<IDocumentSearch>(),
            services.GetRequiredService<IDocumentStatisticsCounter>(), services.GetRequiredService<IStatusBarService>(), null!,
            services.GetRequiredService<ITextCatalog>(),
            new FolioFindServices(services.GetRequiredService<ISearchStories>(), services.GetRequiredService<ISearchReplacing>(),
                services.GetRequiredService<IFolioFindHighlights>(), null!, null!, services.GetRequiredService<ICommentAnchors>()));
}
