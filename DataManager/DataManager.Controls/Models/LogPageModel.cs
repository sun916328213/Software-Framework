using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Log.Definition;
using System.Collections.ObjectModel;
using System.IO;
using WPFCustomControls.Controls;

namespace DataManager.Controls.Models
{
    /// <summary>
    /// 日志测试页状态 Model：继承通用配置管理模型（对标 WPFControl 的 LogConfigModel）。
    /// 行实体为 Extension 的 <see cref="LogInfo"/>；查询条件（时间范围/文件）与结果同住此处。
    /// </summary>
    public partial class LogPageModel : ConfigManagerModel<LogParam>
    {
        public LogPageModel()
        {
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            FileName = "LogConfig.json";
        }

        /// <summary>写入内容。</summary>
        [ObservableProperty]
        private string message = "这是一条测试日志";

        [ObservableProperty]
        private ELogLevel selectedLevel = ELogLevel.Information;

        [ObservableProperty]
        private string? selectedFile;

        [ObservableProperty]
        private ObservableCollection<string> files = new();

        [ObservableProperty]
        private DateTime startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime endDate = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<LogInfo> results = new();

        [ObservableProperty]
        private string multiText = string.Empty;

        [ObservableProperty]
        private LogParam? selectedLogParam;

        partial void OnSelectedLogParamChanged(LogParam? value)
        {
            // 切换档案仅重置文件列表/结果，实例由 Workspace 指纹重建
            Files.Clear();
            SelectedFile = null;
        }

        /// <summary>当前档案（选中行；空即报“请先添加/选择”）。兼容旧调用：无选中时回退首行。</summary>
        public LogParam CurrentProfile() => SelectedProfile();

        public LogParam SelectedProfile() =>
            SelectedLogParam ?? DataCollection.FirstOrDefault() ?? throw new InvalidOperationException("请先添加日志配置档案");
    }
}
