using DataManager.Log.Definition;
using WPFControl.CustomControl.Models;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 日志统一配置模型（仿 CamManagerModel）：数据项为 LogParam（Name/FilePath/SaveDays...），
    /// 由 LogConfigView 内嵌的 DeviceManagerView 编辑并保存到 Config\LogConfig.json。
    /// </summary>
    public class LogConfigModel : ManagerModel<LogParam>
    {
        public LogConfigModel()
        {
            Name = "日志配置";
            FilePath = "Config//";
            FileName = "LogConfig.json";
        }
    }
}
