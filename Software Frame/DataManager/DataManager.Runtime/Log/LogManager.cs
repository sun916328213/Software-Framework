// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LogManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Log、DataManager.Core.Log.Definition、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DataManager.Runtime.Log
{
    /// <summary>
    /// P12：实例创建经 <see cref="ILogProviderFactory"/> 可替换接缝；无参构造默认使用 Runtime 内建工厂，
    /// 旧调用 <c>new LogManager()</c> 与 <c>Create(LogParam)</c> 签名行为保持。
    /// </summary>
    public class LogManager : ManagerBase<ILog, LogParam>, ILogManager
    {
        private readonly ILogProviderFactory _factory;

        /// <summary>
        /// 初始化 LogManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Config。
        /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
        /// </summary>
        public override void Config (List<LogParam> param)
        {
            foreach (var item in param)
            {
                Create(item);
            }
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// 正文中的关键协作调用包括 Create、Add、DisposeWithoutThrow；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetName。
        /// </summary>
        protected override string GetName(ILog device)
        {
          return device.Param.Name;
        }
    }
}
