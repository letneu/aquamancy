namespace Aquamancy.Logic
{
    public interface IDiscordNotifierLogic
    {
        Task SendDiscordMessageAsync(string message);
    }
}
