using DataManager.Log;
using DataManager.Log.Definition;
using Extension.Struct;
using FlowEditor.Core;
using FlowNodes;
using System;
using System.Collections.Generic;
using System.IO;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 流程日志启动器：注册按「流程名\任务名」区分的 DataManager ILog（LogManager），并把节点日志
    /// （NodeLog 显式分级 / ConsoleOutputRouter 捕获的 Console 输出）经 FlowLogContext 路由到
    /// 对应流程任务的日志文件 {FlowLog.FilePath}\{流程名}\{任务名}\yyyyMMdd.txt。
    /// 静态日志（DeviceLog 机台所有 Log / FlowLog / SEMILog）已由 LogConfigService 在配置加载时
    /// 注册到 LogManager，本类只负责 Flow 任务日志的动态注册，FlowLog 基础模板经 LogManager 获取。
    /// 任意「流程名\任务名」首次出现时自动注册；LogManager 经构造器自动注入
    /// （本类注册于 ProgramControl.Services 命名空间，由 RegisterServicesFromNamespace 注册为单例），
    /// 由容器解析后自动 Initialize()。
    /// </summary>
    public class FlowLogBootstrap
    {
        private readonly LogManager _logManager;
        private readonly object _sync = new();

        ILog log => _logManager["FlowLog"];
        public FlowLogBootstrap(LogManager logManager)
        {
            _logManager = logManager;
            Initialize();
        }

        public void Initialize()
        {
            // Console 输出捕获的日志行 → 当前「流程名\任务名」的 ILog（Format 已带 #运行序号 / [节点名] 前缀，保留等级）
            FlowLogContext.FileSink = (line, level) =>
            {
                try
                {
                    var key = FlowLogContext.LogKey;
                    if (string.IsNullOrEmpty(key)) return;
                    Resolve(key)?.Add(line, level);
                }
                catch
                {
                    // 日志失败不影响流程执行
                }
            };

            // 节点显式分级日志（NodeLog.Info/Warn/Error）→ 当前「流程名\任务名」的 ILog
            NodeLog.Resolver = key => Resolve(key);
        }

        /// <summary>解析日志键（流程名\任务名）对应的 ILog；未注册时自动注册，生成两级日志目录。</summary>
        private ILog? Resolve(string? logKey)
        {
            if (string.IsNullOrEmpty(logKey)) return null;

            var existing = _logManager[logKey];
            if (existing != null) return existing;

            lock (_sync)
            {
                existing = _logManager[logKey];
                if (existing != null) return existing;

                // 支持「流程名\任务名」两级目录，各段单独清理非法字符
                var dir = log.Param.FilePath;
                foreach (var part in logKey.Split('\\', StringSplitOptions.RemoveEmptyEntries))
                    dir = Path.Combine(dir, Sanitize(part));

                _logManager.Config(new List<LogParam>
                {
                    BuildParam(log.Param, logKey, dir),
                });
                return _logManager[logKey];
            }
        }

        /// <summary>以模板 LogParam 生成日志参数（继承 Separator/SaveDays/LogType）。</summary>
        private static LogParam BuildParam(LogParam template, string name, string dir)
            => new()
            {
                Name = name,
                FilePath = dir,
                Separator = template.Separator,
                SaveDays = template.SaveDays,
                LogType = template.LogType,
            };

        /// <summary>单个路径段的非法字符替换为下划线。</summary>
        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
