using System.Buffers.Binary;
using PulseOffice.Typography.OpenType;
using Xunit;

namespace PulseOffice.Typography.Tests;

public class MicroTypeFuzzTests
{
    private const long MaximumAllocation = 512L * 1024 * 1024;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    public static TheoryData<string> Fixtures => new() { "boldonse.fntdata", "cff.fntdata", "variable.fntdata" };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void BitAlteratiInQualunquePuntoFallisconoPulitamente(string name)
    {
        var original = Fixture(name);
        var random = new Random(name.Length ^ 8);
        for (var round = 0; round < 200; round++)
        {
            var corrupt = original.ToArray();
            for (var flip = 0; flip < 1 + random.Next(8); flip++)
                corrupt[random.Next(corrupt.Length)] ^= (byte)(1 << random.Next(8));
            AssertFailsCleanly(corrupt);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void BitAlteratiNelFlussoCompressoFallisconoPulitamente(string name)
    {
        var original = Fixture(name);
        var start = original.Length - BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(4));
        var random = new Random(name.Length * 31);
        for (var round = 0; round < 300; round++)
        {
            var corrupt = original.ToArray();
            for (var flip = 0; flip < 1 + random.Next(3); flip++)
                corrupt[random.Next(start, Math.Min(corrupt.Length, start + 64))] ^= (byte)(1 << random.Next(8));
            AssertFailsCleanly(corrupt);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void IFileTroncatiFallisconoPulitamente(string name)
    {
        var original = Fixture(name);
        var start = original.Length - BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(4));
        var random = new Random(41);
        for (var round = 0; round < 60; round++)
        {
            var length = random.Next(start, original.Length);
            var cut = original.AsSpan(0, length).ToArray();
            AssertFailsCleanly(cut);
            BinaryPrimitives.WriteUInt32LittleEndian(cut, (uint)cut.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(cut.AsSpan(4), (uint)(cut.Length - start));
            AssertFailsCleanly(cut);
        }
    }

    private void AssertFailsCleanly(byte[] eot)
    {
        var run = Task.Factory.StartNew(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                new EmbeddedOpenTypeCodec().Decode(eot);
            }
            catch (InvalidDataException)
            {
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }, TaskCreationOptions.LongRunning);

        Assert.True(run.Wait(Deadline), "The decoder did not finish in time.");
        Assert.True(run.Result < MaximumAllocation, $"The decoder allocated {run.Result} bytes.");
    }

    private byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Mtx", name));
}
