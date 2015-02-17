using System.Collections.Generic;
using System.Threading.Tasks;

namespace SetThings
{
    public interface ISettingsStore
    {
        Dictionary<string, string> ReadSettings();
        Task<Dictionary<string, string>> ReadSettingsAsync();
        void WriteSettings(Dictionary<string, string> settings, bool merge = false);
        Task WriteSettingsAsync(Dictionary<string, string> settings, bool merge = false);
    }
}