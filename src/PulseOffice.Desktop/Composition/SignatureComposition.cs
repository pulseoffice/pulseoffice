using PulseOffice.Formats.Intake;
using PulseOffice.Documents;
using PulseOffice.Foundation.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PulseOffice.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using PulseOffice.Sheets;
using PulseOffice.Settings;
using PulseOffice.Foundation.Windowing;
using PulseOffice.Shell;
using PulseOffice.Shell.Surfaces;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Formats.Pdf;
using PulseOffice.Printing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Document.Forms;
using PulseOffice.Security;
using PulseOffice.Text.Scripts;
using PulseOffice.Text.Spelling;
using PulseOffice.Text.Spelling.Hunspell;
using PulseOffice.Sheets.UI;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Theming;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.Text.Rich;
using Avalonia;

namespace PulseOffice.Desktop.Composition;

internal static class SignatureComposition
{
    public static IServiceCollection AddSignatures(this IServiceCollection services)
    {
        services.AddSingleton<IXmlSignatureBuilder, XmlSignatureBuilder>();
        services.AddSingleton<XmlDsigAlgorithms>();
        services.AddSingleton<ICertificateTrust, SystemCertificateTrust>();
        services.AddSingleton<ITrustedTimestampVerifier, TrustedTimestampVerifier>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CertificateJudge>();
        services.AddSingleton<OoxmlSignatureScope>();
        services.AddSingleton<IRelationshipTransform, RelationshipTransform>();
        services.AddSingleton<IOoxmlSignatureBuilder, OoxmlSignatureBuilder>();
        services.AddSingleton<IOpcPackageStore, OpcPackageStore>();
        services.AddSingleton<IPackageSignatureFormat, OdfSignatureService>();
        services.AddSingleton<IPackageSignatureFormat, OoxmlSignatureService>();
        services.AddSingleton<IDocumentSignatureService, DocumentSignatureService>();
        services.AddSingleton<ICertificateSource, CertificateSource>();
        services.AddSingleton<IMacroSigner, MacroSigner>();
        services.AddSingleton<IPdfSignatureFont, PulseOffice.Formats.Pdf.Fonts.EmbeddedPdfSignatureFont>();
        services.AddSingleton<IPdfSigner, PdfSigner>();
        services.AddSingleton<PulseOffice.Shell.ISignedDocumentTracker, PulseOffice.Shell.SignedDocumentTracker>();
        services.AddSingleton<PulseOffice.Shell.SignatureStatusIndicator>();
        services.AddSingleton<LedgerSignatureCommands>();

        return services;
    }
}
