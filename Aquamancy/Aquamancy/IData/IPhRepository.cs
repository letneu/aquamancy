using Aquamancy.Models;

namespace Aquamancy.IData
{
    public interface IPhRepository
    {
        Task<int> AddAsync(PhReading reading);
        Task<IEnumerable<PhReading>> GetForProbeAsync(int probeId, DateTime limit);
        Task EnsureTableExistsAsync();
    }
}
