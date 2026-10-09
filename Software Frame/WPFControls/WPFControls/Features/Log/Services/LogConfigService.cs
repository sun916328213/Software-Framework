// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogConfigService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition、Extension.Core.Conversion、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using Extension.Core.Conversion;
using System.Collections.ObjectModel;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// 日志配置服务（仿 CamManagerService）：统一日志配置（Config\LogConfig.json）的**唯一拥有者** ——
    /// 读取、校验、落盘、生效（Apply）都在这里；页面只负责编辑与转发命令。
    /// 各模块经 IOC 解析 LogManager 后，直接按名称 logManager["..."] 获取 ILog，无需各自注册：
    /// - RunLog（通用运行日志，必须是 logManager[0]，供 LogInfoService 使用）→ DeviceLog 根目录（机台所有 Log）
    /// - FlowLog（供 DataManagerFlowLogSink 作为 Flow 任务日志的基础模板）
    /// - SEMILog（供 SecsHelper 消息记录与 SEMILogQueryView 查询）
    ///
    /// <para>
    /// <b>T12a：保存链路从 View 归还到这里。</b>改造前保存按钮由 <c>ConfigManagerView.SaveData</c> 自己
    /// <c>JsonEx.Save</c>，然后在 try/catch **之外**调 <c>SaveCallback</c> → 本服务的 <see cref="Load"/>：
    /// 写盘失败时照样重载，于是界面刚弹完"保存数据时出错"，运行中的日志实例已经被磁盘上的**旧文件**覆盖回去。
    /// 现在 <see cref="Save"/> 的顺序是 校验 → 落盘 → 生效，前一步失败就**不会**走到下一步。
    /// </para>
    /// </summary>
    public class LogConfigService
    {
        private readonly ILogManager _logManager;
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
        public LogConfigService(ILogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            Load();
        }

        /// <summary>
        /// 从配置文件加载日志配置并使其生效：读 → 校验 → Apply → 刷新 <see cref="Model"/>。
        /// 文件不存在或内容损坏时按空配置处理（<see cref="JsonEx.Read{T}(string, string)"/> 的既有语义）。
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
                Apply(parameters);
                Model.DataCollection = parameters;
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 把 <see cref="Model"/> 里正在编辑的配置**落盘并生效**（校验 → 落盘 → Apply）。
        /// 任一步失败都直接抛出，且**不会**执行后续步骤：校验失败 ⇒ 不写盘、不生效；
        /// 落盘失败 ⇒ 不生效（运行中的日志实例保持原样，磁盘内容也不动）。
        /// </summary>
        /// <exception cref="ArgumentException">某项配置缺必填字段（Name / FilePath / Separator）。</exception>
        /// <exception cref="NotSupportedException">某项配置的日志类型不受支持。</exception>
        /// <exception cref="InvalidOperationException">配置含重名项，或文件路径 / 名称未设置。</exception>
        public void Save()
        {
            lock (_sync)
            {
                var parameters = (Model.DataCollection ?? []).ToList();

                // ① 先校验：坏配置绝不写盘，也就不会被下次启动加载进来。
                Validate(parameters);
                // ② 再落盘：写不进去就到此为止 —— 绝不把磁盘上的旧内容重新 Apply 覆盖运行态。
                WriteFile(parameters);
                // ③ 落盘成功才生效。
                Apply(parameters);
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 把配置写到 <see cref="Model"/> 的 FilePath + FileName；目录不存在时由
        /// <see cref="JsonEx.Save(object, string, string)"/> 补建。写盘失败按原样抛出，不吞。
        /// </summary>
        private void WriteFile(IReadOnlyList<LogParam> parameters)
        {
            if (string.IsNullOrEmpty(Model.FilePath) || string.IsNullOrEmpty(Model.FileName))
                throw new InvalidOperationException("请先设置日志配置的文件路径(FilePath)和文件名称(FileName)。");

            JsonEx.Save(parameters, Model.FilePath, Model.FileName);
        }

        /// <summary>
        /// 让一份**已经校验通过**的配置在运行态生效：整体替换本服务拥有的日志实例，并记下新名称。
        /// <see cref="Load"/> 与 <see cref="Save"/> 共用这一条路径，不另造第二套替换逻辑。
        /// 本方法不负责通知 —— 由调用方在自己的状态更新完成后触发 <see cref="ConfigurationChanged"/>，
        /// 这样 <see cref="Load"/> 的「先替换、再刷新 Model、最后通知」次序与改造前逐字一致。
        /// </summary>
        private void Apply(IReadOnlyList<LogParam> parameters)
        {
            _logManager.ReplaceConfigured(_registeredLogNames, parameters);

            _registeredLogNames.Clear();
            foreach (var parameter in parameters)
                _registeredLogNames.Add(parameter.Name);
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
