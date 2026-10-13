namespace Aquamancy.ILogic
{
    public interface IFirmwareLogic
    {
        IReadOnlyList<string> GetAvailableVersions();
        string? GetFirmwarePath(string version);
        string? GetFirmwareSha256(string version);
        Task<(bool Success, string? ErrorMessage, string? Version)> SaveFirmwareAsync(Stream content);
    }
}
