using System.Security.Cryptography;

namespace DentalClinic.Services;

public static class Tokens
{
    /// <summary>
    /// 128 random bits as url-safe text. Used in the address of a patient's own appointment page,
    /// so it cannot be guessed or counted up the way the short, human-friendly reference (DS-K7M2PQ) could be.
    /// </summary>
    public static string New() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
