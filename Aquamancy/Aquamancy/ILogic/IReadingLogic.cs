using Aquamancy.Dto;
using Aquamancy.Models;

namespace Aquamancy.ILogic
{
    public interface IReadingLogic
    {
        Task<(bool Success, string? ErrorMessage, Probe Probe)> Insert(PostParams postParams);
    }
}