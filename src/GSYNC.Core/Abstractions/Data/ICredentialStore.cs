namespace GSYNC.Core.Abstractions.Data;

public interface ICredentialStore
{
    string Store(IReadOnlyDictionary<string, string> secrets);
    IReadOnlyDictionary<string, string> Read(string reference);
}
