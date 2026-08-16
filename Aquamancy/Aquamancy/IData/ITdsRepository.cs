using Aquamancy.Models;

namespace Aquamancy.IData
{
    public interface ITdsRepository
    {
        Task<int> AddAsync(TdsReading reading);
        Task<IEnumerable<TdsReading>> GetForProbeAsync(int probeId, DateTime limit);
        Task EnsureTableExistsAsync();
    }
}
