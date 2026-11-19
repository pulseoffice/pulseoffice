namespace PulseOffice.Printing;

public enum PrintRole
{
    Section,
    Sheet,
    Slide,
    Title,
    Paragraph,
    Heading,
    List,
    ListItem,
    ListLabel,
    ListBody,
    Table,
    TableRow,
    TableHeader,
    TableCell,
    Figure,
    Link,
    Note,
}

public sealed record PrintTag(string Key, PrintRole Role, PrintTag? Parent = null)
{
    public int Level { get; init; }

    public string? Alternate { get; init; }

    public string? Language { get; init; }

    public IEnumerable<PrintTag> Lineage()
    {
        var chain = new Stack<PrintTag>();
        for (var tag = this; tag is not null; tag = tag.Parent) chain.Push(tag);
        return chain;
    }
}
