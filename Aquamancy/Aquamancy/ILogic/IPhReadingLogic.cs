using Aquamancy.Dto;
using Aquamancy.Models;

namespace Aquamancy.ILogic
{
    public interface IPhReadingLogic
    {
        Task<(bool Success, string? ErrorMessage, Probe Probe)> Insert(PostParams data, Probe probe);
    }
}
