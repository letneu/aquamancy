using Aquamancy.Models;

namespace Aquamancy.IData
{
    public interface ITurbidityRepository
    {
        Task<int> AddAsync(TurbidityReading reading);
        Task<IEnumerable<TurbidityReading>> GetForProbeAsync(int probeId, DateTime limit);
        Task EnsureTableExistsAsync();
    }
}
