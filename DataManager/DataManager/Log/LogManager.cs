using DataManager.Log.Definition;
using DataManager.Log.Log4Net;
using DataManager.Log.NLog;
using DataManager.Log.SeriLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DataManager.Log
{
    public class LogManager : ManagerBase<ILog, LogParam> 
    {
        public override void Config (List<LogParam> param)
        {
            foreach (var item in param)
            {
                Create(item);
            }
        }

        public override ILog Create (LogParam param)
        {
            var log = CreateUnregistered(param);
            try
            {
                Add(log);
                return log;
            }
            catch
            {
                DisposeWithoutThrow(new[] { log }, "注册失败候选日志");
                throw;
            }
        }

        /// <summary>
        /// 原子替换调用方拥有的日志集合；构造或名称校验失败时保留全部旧实例。
        /// </summary>
        /// <param name="ownedNames">调用方当前拥有、允许被替换的日志名称。</param>
        /// <param name="parameters">完整的新日志配置。</param>
        public void ReplaceConfigured(
            IReadOnlyCollection<string> ownedNames,
            IReadOnlyList<LogParam> parameters)
        {
            ArgumentNullException.ThrowIfNull(ownedNames);
            ArgumentNullException.ThrowIfNull(parameters);

            var replacements = new List<ILog>(parameters.Count);
            IReadOnlyList<ILog> removed;
            try
            {
                foreach (var parameter in parameters)
                    replacements.Add(CreateUnregistered(parameter));

                removed = ReplaceOwnedItems(ownedNames, replacements);
            }
            catch
            {
                DisposeWithoutThrow(replacements, "替换失败候选日志");
                throw;
            }

            DisposeWithoutThrow(removed, "已替换旧日志");
        }

        /// <summary>尽力释放日志集合并通过 Trace 报告清理故障，不覆盖原始事务结果。</summary>
        private static void DisposeWithoutThrow(IEnumerable<ILog> logs, string context)
        {
            foreach (var log in logs.OfType<IDisposable>())
            {
                try
                {
                    log.Dispose();
                }
                catch (Exception ex)
                {
                    try
                    {
                        Trace.TraceError($"[LogManager] {context}释放失败: {ex.Message}");
                    }
                    catch
                    {
                        // 清理诊断故障不能覆盖配置提交或原始异常。
                    }
                }
            }
        }

        /// <summary>根据参数创建尚未加入管理器的日志实例。</summary>
        private static ILog CreateUnregistered(LogParam logParam)
        {
            ArgumentNullException.ThrowIfNull(logParam);
            logParam.Validate();

            return logParam.LogType switch
            {
                ELogType.SeriLog => new SerilogHelper { Param = logParam },
                ELogType.NLog => new NLogHelper { Param = logParam },
                ELogType.Log4Net => new Log4NetHelper { Param = logParam },
                _ => throw new NotSupportedException($"{logParam.LogType} not support."),
            };
        }

        protected override string GetName(ILog device)
        {
          return device.Param.Name;
        }
    }
}
