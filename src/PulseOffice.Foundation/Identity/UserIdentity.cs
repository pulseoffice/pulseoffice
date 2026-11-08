namespace PulseOffice.Foundation.Identity;

public interface IUserIdentity
{
    string Name { get; }

    string Initials { get; }
}

public sealed class SystemUserIdentity : IUserIdentity
{
    private readonly InitialsOfName _initials = new();

    public string Name => Environment.UserName;

    public string Initials => _initials.Of(Name);
}

public sealed class InitialsOfName
{
    public string Of(string name)
    {
        var words = name.Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => string.Empty,
            1 => Initial(words[0]),
            _ => string.Concat(words.Take(3).Select(Initial)),
        };
    }

    private string Initial(string word) =>
        System.Globalization.StringInfo.GetNextTextElement(word).ToUpperInvariant();
}
