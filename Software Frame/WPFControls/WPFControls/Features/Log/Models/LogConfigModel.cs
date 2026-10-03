// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogConfigModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、DataManager.Core.Log.Definition、WPFControls.Controls.Models、WPFCustomControls.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls;
using DataManager.Core.Log.Definition;
using WPFControls.Controls.Models;
using WPFCustomControls.Controls;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// 日志统一配置模型（仿 CamManagerModel）：数据项为 LogParam（Name/FilePath/SaveDays...），
    /// 由 LogConfigView 内嵌的 DeviceManagerView 编辑并保存到 Config\LogConfig.json。
    /// </summary>
    public class LogConfigModel : ConfigManagerModel<LogParam>
    {
        /// <summary>
        /// 初始化 LogConfigModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public LogConfigModel()
        {
            Name = "日志配置";
            FilePath = "Config//";
            FileName = "LogConfig.json";
        }
    }
}
