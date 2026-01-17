using Aquamancy.Models;

namespace Aquamancy.IData
{
    public interface IProbeRepository
    {
        Task<int> AddAsync(Probe probe);
        Task<Probe?> GetByIdAsync(int id);
        Task<IEnumerable<Probe>> GetAllAsync();
        Task EnsureTableExistsAsync();
        Task UpdateLastNotifiedAsync(int probeId, System.DateTime? when);
        Task UpdateCommunicationInfoAsync(int probeId, int rssi, System.DateTime lastCommunicationDate);
    }
}
