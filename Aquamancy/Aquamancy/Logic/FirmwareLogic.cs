using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Aquamancy.ILogic;

namespace Aquamancy.Logic
{
    // Les firmwares sont stockés sur disque : <Firmware:Directory>/<version>/main.py
    public partial class FirmwareLogic(IConfiguration configuration, IWebHostEnvironment environment) : IFirmwareLogic
    {
        public const string FirmwareFileName = "main.py";

        private readonly string _directory = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            configuration.GetValue<string>("Firmware:Directory") ?? "Firmwares"));

        // Cache des hash, invalidé si le fichier est modifié
        private readonly ConcurrentDictionary<string, (DateTime LastWrite, string Hash)> _hashCache = new();

        [GeneratedRegex(@"^[0-9A-Za-z._-]{1,50}$")]
        private static partial Regex VersionRegex();

        // Ligne VERSION = "x.y.z" du main.py
        [GeneratedRegex(@"^VERSION\s*=\s*[""']([^""']+)[""']", RegexOptions.Multiline)]
        private static partial Regex FirmwareVersionLineRegex();

        private const long MaxFirmwareSizeInBytes = 512 * 1024;

        public async Task<(bool Success, string? ErrorMessage, string? Version)> SaveFirmwareAsync(Stream content)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory);

            if (memory.Length == 0)
            {
                return (false, "Le fichier est vide.", null);
            }

            if (memory.Length > MaxFirmwareSizeInBytes)
            {
                return (false, "Le fichier est trop volumineux.", null);
            }

            // La version est lue dans le fichier pour qu'elle corresponde toujours à celle envoyée par la sonde
            var text = System.Text.Encoding.UTF8.GetString(memory.ToArray());
            var match = FirmwareVersionLineRegex().Match(text);
            if (!match.Success)
            {
                return (false, "Ligne VERSION = \"x.y.z\" introuvable dans le fichier.", null);
            }

            var version = match.Groups[1].Value.Trim();
            if (!VersionRegex().IsMatch(version) || version.Contains(".."))
            {
                return (false, $"Version invalide : {version}", null);
            }

            if (GetFirmwarePath(version) is not null)
            {
                return (false, $"La version {version} existe déjà, incrémentez VERSION dans main.py.", null);
            }

            var directory = Path.Combine(_directory, version);
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, FirmwareFileName), memory.ToArray());

            return (true, null, version);
        }

        public IReadOnlyList<string> GetAvailableVersions()
        {
            if (!Directory.Exists(_directory))
            {
                return [];
            }

            return Directory.GetDirectories(_directory)
                .Select(Path.GetFileName)
                .Where(v => v is not null && VersionRegex().IsMatch(v) && File.Exists(Path.Combine(_directory, v, FirmwareFileName)))
                .Select(v => v!)
                .OrderByDescending(v => Version.TryParse(v, out var parsed) ? parsed : new Version(0, 0))
                .ThenByDescending(v => v, StringComparer.Ordinal)
                .ToList();
        }

        public string? GetFirmwarePath(string version)
        {
            // Protection contre le path traversal
            if (string.IsNullOrWhiteSpace(version) || !VersionRegex().IsMatch(version) || version.Contains(".."))
            {
                return null;
            }

            var path = Path.GetFullPath(Path.Combine(_directory, version, FirmwareFileName));
            if (!path.StartsWith(_directory, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                return null;
            }

            return path;
        }

        public string? GetFirmwareSha256(string version)
        {
            var path = GetFirmwarePath(version);
            if (path is null)
            {
                return null;
            }

            var lastWrite = File.GetLastWriteTimeUtc(path);
            if (_hashCache.TryGetValue(path, out var cached) && cached.LastWrite == lastWrite)
            {
                return cached.Hash;
            }

            using var stream = File.OpenRead(path);
            var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
            _hashCache[path] = (lastWrite, hash);
            return hash;
        }
    }
}
