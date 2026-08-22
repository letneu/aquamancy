using Aquamancy.Dto;

namespace Aquamancy.ILogic
{
    public interface IAppSettingsWriter
    {
        AppSettingsDto Read();
        void Write(AppSettingsDto settings);
    }
}
