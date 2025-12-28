using Aquamancy.Controllers;
using Aquamancy.Models;

namespace Aquamancy.Logic
{
    public interface ITemperatureReadingLogic
    {
        Task<(bool Success, string ErrorMessage, Probe Probe, double Temperature)> Insert(ApiController.PostParams data);
    }
}