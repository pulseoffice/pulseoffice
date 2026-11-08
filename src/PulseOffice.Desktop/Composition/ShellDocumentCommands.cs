using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Metadata;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellDocumentCommands(
    IDocumentWorkspace workspace,
    IDocumentOperationsCatalog operations,
    IDocumentSharing sharing,
    IDocumentOpenRouter openRouter,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    ITabbedForms forms,
    PulseOffice.Foundation.Identity.IUserIdentity identity)
{
    private readonly SafeFileName _fileNames = new();

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.file.properties"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active is { } document && operations.For(document)?.Properties(document) is not null,
            Execute = (_, ct) => EditPropertiesAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.versions"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => workspace.Active is { } document && operations.For(document)?.Versions(document) is not null,
            Execute = (_, ct) => VersionsAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.share"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            Execute = (_, ct) => ShareAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.edit.editMode"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            Execute = (_, _) =>
            {
                if (workspace.Active is not { } document) return Task.CompletedTask;

                document.IsReadOnly = !document.IsReadOnly;
                status.Report(texts[document.IsReadOnly ? "editMode.readOnly" : "editMode.editable"]);
                return Task.CompletedTask;
            },
        },
    ];

    private async Task VersionsAsync(CancellationToken ct)
    {
        if (workspace.Active is not { } document) return;
        if (operations.For(document) is not { } module || module.Versions(document) is not { } versions) return;

        var action = await dialogs.ShowAsync<PulseOffice.UI.Controls.Prompts.VersionAction>(
            new PulseOffice.UI.Controls.Prompts.VersionsView(
                [.. versions.All.Select(version => new PulseOffice.UI.Controls.Prompts.VersionRow(version.Name, version.Created, version.Author, version.Comment))],
                texts),
            new DialogOptions { TitleKey = "command.shell.file.versions.title", PreferredWidth = 620 },
            ct).ConfigureAwait(true);

        if (action is null) return;

        var chosen = action.Index >= 0 && action.Index < versions.All.Count ? versions.All[action.Index] : null;

        if (chosen is not null && action.Kind == PulseOffice.UI.Controls.Prompts.VersionActionKind.Delete)
        {
            var at = versions.IndexOf(chosen.Name);
            versions.Remove(chosen.Name);
            document.History.Push(new DelegateAction(
                texts["versions.action"],
                () => versions.Remove(chosen.Name),
                () => versions.Restore(at, chosen)));
            status.Report(texts.Format("versions.deleted", chosen.Name));
            return;
        }

        if (chosen is not null && action.Kind == PulseOffice.UI.Controls.Prompts.VersionActionKind.Compare)
        {
            var comparison = await WithVersionFileAsync(chosen, module, path => Task.FromResult(module.CompareVersion(document, path)), ct).ConfigureAwait(true);
            if (comparison is null)
            {
                status.Report(texts.Format("versions.compare.failed", chosen.Name), NotificationSeverity.Error);
                return;
            }

            var answer = await dialogs.ShowAsync<PulseOffice.UI.Controls.Prompts.VersionComparisonAnswer>(
                new PulseOffice.UI.Controls.Prompts.VersionComparisonView(comparison, texts),
                new DialogOptions { TitleKey = "versions.compare.title", TitleArguments = [chosen.Comment.Length > 0 ? chosen.Comment : chosen.Name], PreferredWidth = 760 },
                ct).ConfigureAwait(true);
            if (answer is not { OpenAlongside: true }) return;
            action = action with { Kind = PulseOffice.UI.Controls.Prompts.VersionActionKind.Open };
        }

        if (chosen is not null && action.Kind == PulseOffice.UI.Controls.Prompts.VersionActionKind.Open)
        {
            if (await WithVersionFileAsync(chosen, module, path => openRouter.OpenAsNewAsync(path, ct), ct).ConfigureAwait(true))
                status.Report(texts.Format("versions.opened", chosen.Name));
            return;
        }

        if (action.Kind != PulseOffice.UI.Controls.Prompts.VersionActionKind.Save) return;

        document.CommitPendingEdits();
        var snapshotFolder = Directory.CreateTempSubdirectory("pulseoffice-version-").FullName;
        var snapshot = Path.Combine(snapshotFolder, $"snapshot.{module.NativeExtension}");
        byte[] content;

        try
        {
            if (!module.WriteTo(document, snapshot))
            {
                status.Report(texts["versions.failed"], NotificationSeverity.Error);
                return;
            }

            content = await File.ReadAllBytesAsync(snapshot, ct).ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(snapshotFolder, recursive: true);
        }

        var saved = versions.Add(
            content,
            identity.Name,
            action.Comment);
        var position = versions.IndexOf(saved.Name);

        document.History.Push(new DelegateAction(
            texts["versions.action"],
            () => versions.Restore(position, saved),
            () => versions.Remove(saved.Name)));

        status.Report(texts.Format("versions.saved", saved.Name), NotificationSeverity.Success);
    }

    private async Task ShareAsync(CancellationToken ct)
    {
        if (workspace.Active?.Path is not { Length: > 0 } path)
        {
            status.Report(texts["share.saveFirst"], NotificationSeverity.Warning, NotificationRemedies.Save);
            return;
        }

        if (sharing.Holder(path) is { } held)
        {
            if (!held.IsMine)
            {
                status.Report(texts.Format("share.heldByOther", held.User, held.Machine), NotificationSeverity.Error);
                return;
            }

            var release = await dialogs.ShowAsync<object>(
                new ChoicePromptView(
                    texts["share.releaseQuestion"],
                    texts["share.explanation"],
                    [
                        new PromptChoice(texts["share.release"], true, IsDefault: true),
                        new PromptChoice(texts["prompt.cancel"], false, IsCancel: true),
                    ],
                    texts),
                new DialogOptions { TitleKey = "command.shell.file.share.title", PreferredWidth = 460 },
                ct).ConfigureAwait(true);

            if (release is true)
            {
                var released = sharing.Release(path);
                status.Report(
                    texts[released ? "share.released" : "share.failed"],
                    released ? NotificationSeverity.Info : NotificationSeverity.Error);
            }

            return;
        }

        var claimed = sharing.Claim(path);
        status.Report(
            texts[claimed ? "share.claimed" : "share.failed"],
            claimed ? NotificationSeverity.Info : NotificationSeverity.Error);
    }

    private async Task EditPropertiesAsync(CancellationToken ct)
    {
        if (workspace.Active is not { } document) return;
        if (operations.For(document)?.Properties(document) is not { } properties) return;

        var kinds = Enum.GetValues<CustomPropertyKind>();
        var values = await forms.AskAsync("properties.heading",
        [
            new FormTab(texts["properties.tab.general"], string.Empty,
            [
                new TextField("title", texts["properties.title"], properties.Title ?? string.Empty),
                new TextField("subject", texts["properties.subject"], properties.Subject ?? string.Empty),
                new TextField("author", texts["properties.author"], properties.Author ?? string.Empty),
                new TextField("keywords", texts["properties.keywords"], string.Join(", ", properties.Keywords), texts["properties.keywords.placeholder"]),
            ]),
            new FormTab(texts["properties.tab.description"], string.Empty,
            [
                new TextField("description", texts["properties.description"], properties.Description ?? string.Empty) { Multiline = true },
            ]),
            new FormTab(texts["properties.tab.custom"], texts["properties.custom.explanation"],
            [
                new PulseOffice.UI.Controls.Forms.CustomPropertiesField("custom", texts["properties.tab.custom"],
                    [.. properties.Custom.Select(entry => new PulseOffice.UI.Controls.Forms.CustomPropertyItem(entry.Key, entry.Value, Array.IndexOf(kinds, properties.KindOf(entry.Key))))],
                    [.. kinds.Select(kind => texts[$"properties.custom.kind.{kind.ToString().ToLowerInvariant()}"])]),
            ]),
            new FormTab(texts["properties.tab.statistics"], string.Empty, [.. Statistics(document, properties)]),
        ], ct).ConfigureAwait(true);

        if (values is null) return;

        if (document.IsReadOnly)
        {
            status.Report(texts["status.readOnly"], NotificationSeverity.Error, NotificationRemedies.EditMode);
            return;
        }

        var before = Copy(properties);
        var after = Copy(properties);

        after.Title = Text(values["title"]);
        after.Subject = Text(values["subject"]);
        after.Author = Text(values["author"]);
        after.Description = Text(values["description"]);
        after.Keywords.Clear();
        after.Custom.Clear();
        after.CustomKinds.Clear();
        if (values["custom"] is IEnumerable<PulseOffice.UI.Controls.Forms.CustomPropertyItem> custom)
            foreach (var item in custom)
            {
                after.Custom[item.Name] = item.Value;
                if (kinds[Math.Clamp(item.Kind, 0, kinds.Length - 1)] is var kind and not CustomPropertyKind.Text) after.CustomKinds[item.Name] = kind;
            }

        if (values["keywords"] as string is { Length: > 0 } keywords)
            after.Keywords.AddRange(
                keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (Same(before, after)) return;

        properties.CopyFrom(after);
        document.History.Push(new DelegateAction(
            texts["properties.action"],
            () => properties.CopyFrom(after),
            () => properties.CopyFrom(before)));
    }

    private static DocumentProperties Copy(DocumentProperties source)
    {
        var copy = new DocumentProperties();
        copy.CopyFrom(source);
        return copy;
    }

    private static bool Same(DocumentProperties left, DocumentProperties right) =>
        left.Title == right.Title
        && left.Subject == right.Subject
        && left.Author == right.Author
        && left.Description == right.Description
        && left.Keywords.SequenceEqual(right.Keywords, StringComparer.Ordinal)
        && left.Custom.OrderBy(entry => entry.Key, StringComparer.Ordinal).SequenceEqual(right.Custom.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        && left.CustomKinds.OrderBy(entry => entry.Key, StringComparer.Ordinal).SequenceEqual(right.CustomKinds.OrderBy(entry => entry.Key, StringComparer.Ordinal));

    private IEnumerable<FormField> Statistics(OpenDocument document, DocumentProperties properties)
    {
        var unknown = texts["properties.unknown"];
        string When(DateTimeOffset? moment) => moment?.LocalDateTime.ToString("g", System.Globalization.CultureInfo.CurrentCulture) ?? unknown;

        yield return new PulseOffice.UI.Controls.Forms.InfoField("file", texts["properties.file"], document.Path ?? texts["properties.notSaved"]);
        if (document.Path is { Length: > 0 } path && File.Exists(path))
            yield return new PulseOffice.UI.Controls.Forms.InfoField("size", texts["properties.size"], texts.Format("properties.size.kb", Math.Max(1, new FileInfo(path).Length / 1024)));
        yield return new PulseOffice.UI.Controls.Forms.InfoField("created", texts["properties.createdOn"], When(properties.Created));
        yield return new PulseOffice.UI.Controls.Forms.InfoField("modified", texts["properties.modifiedOn"], When(properties.Modified));
        yield return new PulseOffice.UI.Controls.Forms.InfoField("modifiedBy", texts["properties.modifiedBy"], properties.LastModifiedBy ?? unknown);
        yield return new PulseOffice.UI.Controls.Forms.InfoField("cycles", texts["properties.editingCycles"], properties.EditingCycles.ToString(System.Globalization.CultureInfo.CurrentCulture));
        yield return new PulseOffice.UI.Controls.Forms.InfoField("generator", texts["properties.generator"], properties.Generator ?? unknown);
    }

    private static string? Text(object value) =>
        value as string is { Length: > 0 } text ? text : null;

    private async Task<T> WithVersionFileAsync<T>(PulseOffice.Document.Versions.DocumentVersion version, IDocumentOperations module, Func<string, Task<T>> use, CancellationToken ct)
    {
        var folder = Directory.CreateTempSubdirectory("pulseoffice-version-").FullName;
        var path = Path.Combine(folder, $"{_fileNames.Of(version.Name, "version")}.{module.NativeExtension}");
        try
        {
            await File.WriteAllBytesAsync(path, version.Content, ct).ConfigureAwait(true);
            return await use(path).ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
