using DataManager.Log.Definition;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DataManager.Log
{
    /// <summary>
    /// P12：实例创建经 <see cref="ILogProviderFactory"/> 可替换接缝；无参构造默认使用 Runtime 内建工厂，
    /// 旧调用 <c>new LogManager()</c> 与 <c>Create(LogParam)</c> 签名行为保持。
    /// </summary>
    public class LogManager : ManagerBase<ILog, LogParam>, ILogManager
    {
        private readonly ILogProviderFactory _factory;

        public LogManager()
            : this(new LogProviderFactory())
        {
        }

        /// <summary>
        /// 使用可注入的 Provider 工厂创建管理器；测试可传入 Fake/Stub，无需真实日志后端。
        /// </summary>
        public LogManager(ILogProviderFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public override void Config (List<LogParam> param)
        {
            foreach (var item in param)
            {
                Create(item);
            }
        }

        public override ILog Create (LogParam param)
        {
            var log = _factory.Create(param);
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
                    replacements.Add(_factory.Create(parameter));

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

        protected override string GetName(ILog device)
        {
          return device.Param.Name;
        }
    }
}
