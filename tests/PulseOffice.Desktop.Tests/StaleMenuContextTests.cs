using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using PulseOffice.Shell.Surfaces;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public class StaleMenuContextTests(HeadlessDesktopSession headless)
{
    public class Inert : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(string) ? args?.FirstOrDefault()?.ToString() ?? "text"
            : targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void) ? Activator.CreateInstance(type) : null;
    }

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, Inert>();

    [Fact]
    public Task A_menu_item_built_before_the_document_became_read_only_does_not_edit_it() => headless.Session.Dispatch(() =>
    {
        var workspace = new DocumentWorkspace();
        var registry = new CommandRegistry(Fake<PulseOffice.Localization.ITextCatalog>());
        var ran = 0;
        registry.Register(new CommandDefinition
        {
            Id = new CommandId("test.edit"),
            Module = ModuleKind.Sheets,
            Surface = CommandSurface.ContextMenu,
            ContextPaths = ["cell/Edit/Change"],
            ForbiddenState = DocumentState.ReadOnly,
            Execute = (_, _) =>
            {
                ran++;
                return Task.CompletedTask;
            },
        });
        var executor = new CommandExecutor(
            Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(), Fake<PulseOffice.UI.Controls.Notifications.INotificationHistory>(),
            registry, Fake<PulseOffice.Localization.ITextCatalog>(), Fake<ICommandRecorder>(), new CommandEffectChecks([]),
            NullLogger<CommandExecutor>.Instance);
        var factory = new CommandSurfaceFactory(
            registry, new RibbonProjection(registry, Fake<IRibbonDisplayMap>()), executor, workspace, new MenuPlanner(),
            Fake<PulseOffice.UI.Controls.Text.ITextEditingRouter>());
        var document = new OpenDocument(ModuleKind.Sheets, "Conti", new Panel(), new UndoStack());
        workspace.Open(document);

        var menu = factory.Build("cell");
        Assert.NotNull(menu);
        var item = menu.Items.OfType<MenuItem>().Single(candidate => candidate.IsEnabled && candidate.Focusable);
        while (item.Items.OfType<MenuItem>().FirstOrDefault() is { } child) item = child;

        document.IsReadOnly = true;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal(0, ran);
    }, TestContext.Current.CancellationToken);
}
