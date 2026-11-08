using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Localization;
using PulseOffice.Presentations.UI;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DeckFormulaPromptTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task The_formula_prompt_closes_when_focus_leaves_it_and_never_stacks() => _session.Dispatch(async () =>
    {
        var dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-deck-prompt-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, dataHome);
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        try
        {
            window.Show();
            var workspace = provider.GetRequiredService<IDocumentWorkspace>();
            var executor = provider.GetRequiredService<CommandExecutor>();
            await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
            Dispatcher.UIThread.RunJobs();
            var placeholder = provider.GetRequiredService<ITextCatalog>()["deck.formula.placeholder"];
            List<TextBox> Prompts() => [.. window.GetVisualDescendants().OfType<TextBox>().Where(box => box.PlaceholderText == placeholder)];

            async Task Insert()
            {
                await executor.ExecuteAsync(registry.Find(new CommandId("deck.insert.formula"))!, workspace.Context);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }

            await Insert();
            await Insert();
            var prompt = Assert.Single(Prompts());
            Assert.True(prompt.IsFocused);

            var elsewhere = window.GetVisualDescendants().OfType<Control>()
                .First(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control is not TextBox && !prompt.GetVisualAncestors().Contains(control));
            elsewhere.Focus();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(Prompts());
            Assert.Empty(provider.GetRequiredService<IActiveDeckContext>().Target!.CurrentSlide()!.Elements.OfType<PulseOffice.Presentations.ObjectElement>());
        }
        finally
        {
            window.Close();
            await provider.DisposeAsync();
            Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
            if (Directory.Exists(dataHome)) Directory.Delete(dataHome, recursive: true);
        }
    }, CancellationToken.None);
}
