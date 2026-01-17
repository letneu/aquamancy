namespace Aquamancy.ILogic
{
    public interface IDiscordNotifierLogic
    {
        Task SendDiscordMessageAsync(string message);
    }
}
