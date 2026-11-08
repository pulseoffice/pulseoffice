using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

public sealed class AllocationSampler : EventListener
{
    private readonly ConcurrentDictionary<string, long> _bytesByType = new();

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)0x1);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName != "GCAllocationTick_V4" || eventData.Payload is null) return;

        var type = eventData.Payload[eventData.PayloadNames!.IndexOf("TypeName")] as string ?? "?";
        var amount = Convert.ToInt64(eventData.Payload[eventData.PayloadNames.IndexOf("AllocationAmount64")]);
        _bytesByType.AddOrUpdate(type, amount, (_, total) => total + amount);
    }

    public void Print(int top)
    {
        foreach (var (type, bytes) in _bytesByType.OrderByDescending(pair => pair.Value).Take(top))
            Console.WriteLine($"{bytes / 1024,10} KB  {type}");
    }
}
