using Aquamancy.ILogic;
using static System.Net.Mime.MediaTypeNames;

namespace Aquamancy.Logic
{
    public class ErrorTriggerLogic(IConfiguration configuration, ILogger<ErrorTriggerLogic> logger) : IErrorTriggerLogic
    {
        public Exception? LastException { get; private set; }
        public string? LastInfoMessage { get; private set; }
        public bool HasError => LastException != null || LastInfoMessage != null;
        private readonly bool _isEnabled = configuration.GetValue<bool>("ErrorOnGui:ErrorOnGuiEnabled");
        private readonly int _timeBeforeClearingErrorsInMinutes = configuration.GetValue<int>("ErrorOnGui:TimeBeforeClearingErrorsInMinutes");
        private Timer? _timer;
        private readonly ILogger<ErrorTriggerLogic> _logger = logger;

        public void TriggerError(Exception ex, string infoMessage)
        {
            if (!_isEnabled)
            {
                return;
            }

            _logger.LogError(ex, "Error triggered: {InfoMessage}", infoMessage);

            LastException = ex;
            LastInfoMessage = infoMessage;

            // Reset the timer
            _timer?.Dispose();
            _timer = new Timer(OnTimedEvent, null, TimeSpan.FromMinutes(_timeBeforeClearingErrorsInMinutes), TimeSpan.FromMinutes(_timeBeforeClearingErrorsInMinutes));
        }

        private void OnTimedEvent(object? state)
        {
            LastException = null;
            LastInfoMessage = null;
        }
    }
}
