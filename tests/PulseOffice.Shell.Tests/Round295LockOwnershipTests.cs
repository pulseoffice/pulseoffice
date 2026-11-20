using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round295LockOwnershipTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulse-lock-295-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class Ownership(int process, ISet<int> running) : IRecoveryOwnership
    {
        public RecoveryOwner Current { get; } = new(process, DateTimeOffset.UnixEpoch);

        public bool IsAlive(RecoveryOwner owner) => running.Contains(owner.ProcessId);
    }

    private string Document(string lockText)
    {
        var document = Path.Combine(_folder, "Report.ods");
        File.WriteAllText(FileDocumentSharing.LockPathOf(document), lockText);
        return document;
    }

    private string Mine(string process) =>
        $",{Environment.UserName},{Environment.MachineName},03.10.2026 14:05,{process};";

    [Fact]
    public void A_lock_of_the_same_account_without_our_process_belongs_to_another_program()
    {
        var document = Document(Mine("file:///home/someone/.config/suite/4"));

        Assert.False(new FileDocumentSharing(new Ownership(10, new HashSet<int>())).Holder(document)!.IsMine);
    }

    [Fact]
    public void A_lock_of_another_live_instance_is_not_ours_but_a_dead_one_is()
    {
        var document = Document(Mine("pulseoffice-process:20:0"));

        Assert.False(new FileDocumentSharing(new Ownership(10, new HashSet<int> { 20 })).Holder(document)!.IsMine);
        Assert.True(new FileDocumentSharing(new Ownership(10, new HashSet<int>())).Holder(document)!.IsMine);
    }

    [Fact]
    public void Our_own_claim_round_trips_as_ours()
    {
        var document = Path.Combine(_folder, "Own.ods");
        var sharing = new FileDocumentSharing(new Ownership(10, new HashSet<int> { 10 }));

        Assert.True(sharing.Claim(document));
        var holder = sharing.Holder(document)!;

        Assert.True(holder.IsMine);
        Assert.Equal(10, holder.Process!.ProcessId);
        Assert.True(sharing.Release(document));
    }

    [Fact]
    public void Claiming_over_a_lock_left_by_our_dead_process_rewrites_it_with_the_current_process()
    {
        var document = Document(Mine("pulseoffice-process:20:0"));
        var sharing = new FileDocumentSharing(new Ownership(10, new HashSet<int> { 10 }));

        Assert.True(sharing.Holder(document)!.IsStale);
        Assert.True(sharing.Claim(document));

        Assert.False(sharing.Holder(document)!.IsStale);
        Assert.Equal(10, sharing.Holder(document)!.Process!.ProcessId);
    }

    [Fact]
    public void Claiming_over_a_lock_of_another_live_instance_fails_and_keeps_it()
    {
        var document = Document(Mine("pulseoffice-process:20:0"));
        var sharing = new FileDocumentSharing(new Ownership(10, new HashSet<int> { 10, 20 }));

        Assert.False(sharing.Claim(document));

        Assert.Equal(20, sharing.Holder(document)!.Process!.ProcessId);
    }
}
