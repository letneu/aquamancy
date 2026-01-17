namespace Aquamancy.ILogic
{
    public interface IErrorTriggerLogic
    {
        bool HasError { get; }
        Exception? LastException { get; }
        string? LastInfoMessage { get; }

        void TriggerError(Exception ex, string infoMessage);
    }
}