namespace PulseOffice.Documents.Revisions;

public sealed record RevisionSignature(string Author, DateTimeOffset Date);

public interface IRevisionAuthor
{
    RevisionSignature Sign();
}

public sealed class IdentityRevisionAuthor(PulseOffice.Foundation.Identity.IUserIdentity identity) : IRevisionAuthor
{
    public RevisionSignature Sign() => new(identity.Name, DateTimeOffset.Now);
}
