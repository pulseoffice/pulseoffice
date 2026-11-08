using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Typography.Sources;

public interface IFontSource
{
    string Id { get; }

    FontOrigin Origin { get; }

    bool IsVolatile => false;

    Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> FetchAsync(
        FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default);
}

public interface IInstalledFontNames
{
    IReadOnlyList<string> Families { get; }
}
