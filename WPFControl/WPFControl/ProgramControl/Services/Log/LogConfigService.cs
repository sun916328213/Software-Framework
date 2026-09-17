using DataManager.Log;
using DataManager.Log.Definition;
using Extension.Core.ConvertEx;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 日志配置服务（仿 CamManagerService）：加载统一日志配置（Config\LogConfig.json），
    /// 并在配置时自动把日志注册到 LogManager。各模块经 IOC 解析 LogManager 后，
    /// 直接按名称 logManager["..."] 获取 ILog，无需各自注册：
    /// - RunLog（通用运行日志，必须是 logManager[0]，供 LogInfoService 使用）→ DeviceLog 根目录（机台所有 Log）
    /// - FlowLog（供 DataManagerFlowLogSink 作为 Flow 任务日志的基础模板）
    /// - SEMILog（供 SecsHelper 消息记录与 SEMILogQueryView 查询）
    /// 数据写入由 LogConfigView 内嵌的 DeviceManagerView 完成（保存按钮内部 JsonEx.Save），
    /// 保存后重载（SaveCallback → Load）会先移除旧实例再重新注册，使修改的路径/参数即时生效。
    /// </summary>
    public class LogConfigService
    {
        private readonly LogManager _logManager;
        private readonly object _sync = new();
        private readonly HashSet<string> _registeredLogNames = new(StringComparer.Ordinal);

        /// <summary>
        /// 基础日志配置成功替换后触发；动态日志 adapter 可据此丢弃旧路径实例。
        /// </summary>
        public event EventHandler? ConfigurationChanged;

        /// <summary>
        /// 配置模型（文件路径 + 数据集合）。
        /// </summary>
        public LogConfigModel Model { get; set; } = new LogConfigModel();

        /// <summary>
        /// 当前名为 FlowLog 的基础日志模板；未配置时为空。与 RunLog / SEMILog 同属整机级配置项。
        /// </summary>
        public LogParam? FlowLogTemplate => Model.DataCollection
            .FirstOrDefault(parameter => parameter.Name == "FlowLog");

        /// <summary>创建日志配置服务并加载已注入的日志管理器。</summary>
        /// <param name="logManager">宿主共享的日志管理器。</param>
        public LogConfigService(LogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            Load();
        }

        /// <summary>
        /// 从配置文件加载日志配置，并自动注册（配置时自动注册，见 <see cref="RegisterServices"/>）。
        /// </summary>
        public void Load()
        {
            lock (_sync)
            {
                var parameters = JsonEx.Read<ObservableCollection<LogParam>>(
                    Model.FilePath ?? string.Empty,
                    Model.FileName ?? string.Empty)
                    ?? new ObservableCollection<LogParam>();

                Validate(parameters);
                _logManager.ReplaceConfigured(_registeredLogNames, parameters.ToList());

                _registeredLogNames.Clear();
                foreach (var parameter in parameters)
                    _registeredLogNames.Add(parameter.Name);
                Model.DataCollection = parameters;

                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 在释放当前日志前校验新配置，避免错误配置导致已运行日志被提前移除。
        /// </summary>
        private static void Validate(IEnumerable<LogParam> parameters)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                parameter.Validate();
                if (!Enum.IsDefined(parameter.LogType))
                    throw new NotSupportedException($"{parameter.LogType} not support.");
                if (!names.Add(parameter.Name))
                    throw new InvalidOperationException($"日志配置包含重名项 {parameter.Name}");
            }
        }

    }
}

