using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class ScriptTranscriptTests
{
    [Fact]
    public void A_macro_that_logs_without_end_cannot_grow_the_output_beyond_its_limit()
    {
        var transcript = new ScriptTranscript(1000);

        for (var index = 0; index < 10_000; index++) transcript.Add(new string('x', 300));

        Assert.True(transcript.Lines.Sum(line => line.Length) <= 1000);
        Assert.True(transcript.Truncated);
    }

    [Fact]
    public void Output_within_the_limit_is_kept_whole()
    {
        var transcript = new ScriptTranscript(1000);

        transcript.Add("uno");
        transcript.Add("due");

        Assert.Equal(["uno", "due"], transcript.Lines);
        Assert.False(transcript.Truncated);
    }

    [Fact]
    public void A_line_that_crosses_the_limit_is_cut_at_the_limit()
    {
        var transcript = new ScriptTranscript(5);

        transcript.Add("abc");
        transcript.Add("defgh");

        Assert.Equal(["abc", "de"], transcript.Lines);
        Assert.True(transcript.Truncated);
    }
}
