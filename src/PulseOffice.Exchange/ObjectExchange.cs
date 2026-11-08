using Microsoft.Extensions.Logging;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Exchange;

public interface IExchangeParticipant
{
    ModuleKind Module { get; }

    bool CanCapture { get; }

    bool CanReceive { get; }

    ExchangePackage? Capture(string id, bool cut);

    IReadOnlyList<ExchangePasteMode> Modes(ExchangePackage package);

    bool Receive(ExchangePackage package, ExchangePasteMode mode);
}

public interface IObjectExchange
{
    ExchangePackage? Last { get; }

    bool CanCopy(ModuleKind module);

    bool CanPaste(ModuleKind module);

    Task<ExchangePackage?> CopyAsync(ModuleKind module, CancellationToken ct = default);

    Task<ExchangePackage?> CutAsync(ModuleKind module, CancellationToken ct = default);

    Task PublishAsync(ExchangePackage package, CancellationToken ct = default);

    Task<ExchangePackage> CurrentAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ExchangePasteMode>> ModesAsync(ModuleKind module, CancellationToken ct = default);

    Task<bool> PasteAsync(ModuleKind module, ExchangePasteMode? mode = null, CancellationToken ct = default);

    ExchangePackage? Capture(ModuleKind module);

    IReadOnlyList<ExchangePasteMode> Modes(ModuleKind module, ExchangePackage package);

    bool Receive(ModuleKind module, ExchangePackage package, ExchangePasteMode? mode = null);

    IReadOnlyList<ExchangeRepresentation> Publishable(ExchangePackage package);

    ExchangePackage Recognize(IReadOnlyList<ExchangeRepresentation> representations);
}

public sealed class ObjectExchange(
    IEnumerable<IExchangeParticipant> participants,
    IExchangeClipboard clipboard,
    IExchangeSerializer serializer,
    ILogger<ObjectExchange> logger) : IObjectExchange
{
    private readonly IReadOnlyList<IExchangeParticipant> _participants = [.. participants];
    private ExchangePackage? _last;

    public ExchangePackage? Last => _last;

    public bool CanCopy(ModuleKind module) => Participant(module)?.CanCapture == true;

    public bool CanPaste(ModuleKind module) =>
        Participant(module)?.CanReceive == true && (_last is not null || clipboard.IsAvailable);

    public Task<ExchangePackage?> CopyAsync(ModuleKind module, CancellationToken ct = default) => PublishAsync(module, cut: false, ct);

    public Task<ExchangePackage?> CutAsync(ModuleKind module, CancellationToken ct = default) => PublishAsync(module, cut: true, ct);

    public async Task<ExchangePackage> CurrentAsync(CancellationToken ct = default)
    {
        var representations = clipboard.IsAvailable ? await clipboard.ReadAsync(ct).ConfigureAwait(true) : [];
        if (representations.Count == 0) return _last ?? Empty();
        return Recognize(representations);
    }

    public async Task<IReadOnlyList<ExchangePasteMode>> ModesAsync(ModuleKind module, CancellationToken ct = default) =>
        Modes(module, await CurrentAsync(ct).ConfigureAwait(true));

    public async Task<bool> PasteAsync(ModuleKind module, ExchangePasteMode? mode = null, CancellationToken ct = default)
    {
        if (Participant(module) is not { CanReceive: true }) return false;
        var package = await CurrentAsync(ct).ConfigureAwait(true);
        return Receive(module, package, mode);
    }

    public ExchangePackage? Capture(ModuleKind module) =>
        Participant(module) is { CanCapture: true } participant ? participant.Capture(NewId(), cut: false) : null;

    public IReadOnlyList<ExchangePasteMode> Modes(ModuleKind module, ExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return package.IsEmpty || Participant(module) is not { } participant ? [] : participant.Modes(package);
    }

    public bool Receive(ModuleKind module, ExchangePackage package, ExchangePasteMode? mode = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (Participant(module) is not { CanReceive: true } participant) return false;
        var modes = participant.Modes(package);
        if (modes.Count == 0) return false;
        if (mode is { } requested && !modes.Contains(requested)) return false;
        var chosen = mode ?? modes[0];
        logger.LogInformation("Pasting exchange package {Id} from {Origin} into {Module} as {Mode}", package.Id, package.Origin, module, chosen);
        return participant.Receive(package, chosen);
    }

    public IReadOnlyList<ExchangeRepresentation> Publishable(ExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var native = ExchangeRepresentation.OfText(ExchangeFlavor.Native, string.Empty) with { Data = serializer.Write(package) };
        return [native, .. package.Representations.Where(representation => representation.Flavor != ExchangeFlavor.Native)];
    }

    public ExchangePackage Recognize(IReadOnlyList<ExchangeRepresentation> representations)
    {
        ArgumentNullException.ThrowIfNull(representations);
        var native = representations.FirstOrDefault(representation => representation.Flavor == ExchangeFlavor.Native);
        var described = native is null ? null : serializer.Read(native.Data);
        if (described is not null && _last is { } last && last.Id == described.Id) return last;
        var foreign = representations.Where(representation => representation.Flavor != ExchangeFlavor.Native).ToList();
        return described is null
            ? new ExchangePackage { Id = NewId(), Representations = foreign }
            : described with { Representations = foreign };
    }

    private async Task<ExchangePackage?> PublishAsync(ModuleKind module, bool cut, CancellationToken ct)
    {
        if (Participant(module) is not { CanCapture: true } participant) return null;
        if (participant.Capture(NewId(), cut) is not { IsEmpty: false } package) return null;
        await PublishAsync(package, ct).ConfigureAwait(true);
        return package;
    }

    public async Task PublishAsync(ExchangePackage package, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        _last = package;
        if (clipboard.IsAvailable) await clipboard.WriteAsync(Publishable(package), ct).ConfigureAwait(true);
        logger.LogInformation("Published exchange package {Id} from {Origin} with {Count} representations", package.Id, package.Origin, package.Representations.Count);
    }

    private IExchangeParticipant? Participant(ModuleKind module) => _participants.FirstOrDefault(participant => participant.Module == module);

    private ExchangePackage Empty() => new() { Id = NewId() };

    private string NewId() => Guid.NewGuid().ToString("N");
}
