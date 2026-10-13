namespace Aquamancy.Dto
{
    public record PostParams(string MachineName, string Temperature, string? Tds, int Rssi, bool FirstLoop, string? Version = null, string? RolledBackFrom = null);
}
