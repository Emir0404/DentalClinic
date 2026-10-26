using Microsoft.AspNetCore.Http;

namespace DentalClinic.Services;

public record PhotoResult(string? FileName, string? Error)
{
    public bool Succeeded => FileName is not null;
}

/// <summary>
/// Čuva fotografije koje admin otprema (doktori). Ne vjeruje imenu ni tipu koje pošalje browser:
/// vrstu fajla određuje po prvim bajtovima, a ime fajla pravi sam, pa niko ne može poslati
/// "slika.jpg.exe" ili "../../neki-fajl".
/// </summary>
public class PhotoStorage
{
    public const long MaxBytes = 4 * 1024 * 1024;
    private readonly string _folder;

    public PhotoStorage(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _folder = config["UploadsPath"] is { Length: > 0 } custom ? custom : Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "img", "uploads");
    }

    public async Task<PhotoResult> SaveAsync(IFormFile file, string namePrefix, CancellationToken ct = default)
    {
        if (file.Length == 0) return new(null, "Odabrana datoteka je prazna.");
        if (file.Length > MaxBytes) return new(null, "Fotografija je prevelika (najviše 4 MB).");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var extension = DetectExtension(header.AsSpan(0, read));
        if (extension is null) return new(null, "Dozvoljeni su samo JPG, PNG i WEBP fajlovi.");

        Directory.CreateDirectory(_folder);
        var prefix = namePrefix.Length > 40 ? namePrefix[..40] : namePrefix;
        var name = $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)] + extension;
        await using (var output = File.Create(Path.Combine(_folder, name)))
        {
            output.Write(header, 0, read);
            await input.CopyToAsync(output, ct);
        }
        return new(name, null);
    }

    /// <summary>Briše samo fotografije koje je admin otpremio (imaju nastavak), nikad slike iz početnog sadržaja.</summary>
    public void Delete(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName) || !fileName.Contains('.') || fileName != Path.GetFileName(fileName)) return;
        var path = Path.Combine(_folder, fileName);
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { /* nije bitno ako ostane */ }
    }

    public static string? DetectExtension(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h.Length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
    }
}
