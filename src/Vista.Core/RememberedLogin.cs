namespace Vista.Core;

// Contains a revocable session credential, never the pilot's password.
public sealed record RememberedLogin(string ProjectUrl, string Identifier, string RefreshToken);

public interface ILoginStore
{
    RememberedLogin? Load();
    void Save(RememberedLogin login);
    void Clear();
}
