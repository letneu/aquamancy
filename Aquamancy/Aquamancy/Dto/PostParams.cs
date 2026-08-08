namespace Aquamancy.Dto
{
    public record PostParams(string MachineName, string Temperature, string? Turbidity, int Rssi, bool FirstLoop);
}
