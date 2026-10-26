using System.Security.Cryptography;
using System.Text;

namespace DentalClinic.Services;

public class AdminOptions
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

/// <summary>
/// Provjera admin prijave. Korisničko ime i lozinka dolaze iz konfiguracije (user-secrets / environment varijable),
/// nikad iz koda. Ako lozinka nije podešena, admin panel je isključen (niko se ne može prijaviti).
/// </summary>
public class AdminAuth
{
    private readonly AdminOptions _options;
    public AdminAuth(Microsoft.Extensions.Options.IOptions<AdminOptions> options) => _options = options.Value;

    public bool Enabled => _options.Username.Length > 0 && _options.Password.Length > 0;

    public bool Validate(string? username, string? password)
    {
        if (!Enabled) return false;
        // hash-ovi iste dužine + FixedTimeEquals: vrijeme provjere ne odaje koliko je znakova tačno
        var userOk = Same(username ?? "", _options.Username);
        var passOk = Same(password ?? "", _options.Password);
        return userOk & passOk;
    }

    private static bool Same(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
