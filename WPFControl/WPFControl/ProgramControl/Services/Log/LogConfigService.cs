using DataManager.Log;
using DataManager.Log.Definition;
using Extension.Core.ConvertEx;
using Prism.Ioc;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 日志配置服务（仿 CamManagerService）：加载统一日志配置（Config\LogConfig.json），
    /// 并在配置时自动把日志注册到 LogManager。各模块经 IOC 解析 LogManager 后，
    /// 直接按名称 logManager["..."] 获取 ILog，无需各自注册：
    /// - RunLog（通用运行日志，必须是 logManager[0]，供 LogInfoService 使用）→ DeviceLog 根目录（机台所有 Log）
    /// - FlowLog（供 FlowLogBootstrap 作为 Flow 任务日志的基础模板）
    /// - SEMILog（供 SecsHelper 消息记录与 SEMILogQueryView 查询）
    /// 数据写入由 LogConfigView 内嵌的 DeviceManagerView 完成（保存按钮内部 JsonEx.Save），
    /// 保存后重载（SaveCallback → Load）会先移除旧实例再重新注册，使修改的路径/参数即时生效。
    /// </summary>
    public class LogConfigService
    {
        private LogManager _logManager;

        /// <summary>
        /// 配置模型（文件路径 + 数据集合）
        /// </summary>
        public LogConfigModel Model { get; set; } = new LogConfigModel();

        public LogConfigService( IContainerProvider container)
        {
            _logManager = container.Resolve<LogManager>();
            Load();

            ///初始化 FlowLogBootstrap，确保流程日志注册到 LogManager
            container.Resolve<FlowLogBootstrap>().Initialize();
        }

        /// <summary>
        /// 从配置文件加载日志配置，并自动注册（配置时自动注册，见 <see cref="RegisterServices"/>）。
        /// </summary>
        public void Load()
        {
            Model.DataCollection = JsonEx.Read<ObservableCollection<LogParam>>(Model.FilePath, Model.FileName)
                ?? new ObservableCollection<LogParam>();
            _logManager.Config(Model.DataCollection.ToList());
        }
    }
}
