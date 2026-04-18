namespace Aquamancy.Dto
{
    public record PostParams(string MachineName, string Temperature, string Ph, int Rssi, bool FirstLoop);
}
