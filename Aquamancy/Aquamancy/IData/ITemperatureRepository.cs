using Aquamancy.Models;

namespace Aquamancy.IData
{
    public interface ITemperatureRepository
    {
        Task<int> AddAsync(TemperatureReading reading);
        Task<IEnumerable<TemperatureReading>> GetForProbeAsync(int probeId, DateTime limit);
        Task EnsureTableExistsAsync();
    }
}
