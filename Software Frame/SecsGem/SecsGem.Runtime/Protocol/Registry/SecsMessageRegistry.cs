// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsMessageRegistry。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、SecsGem.Runtime.Protocol、SecsGem.Core.Order、SecsGem.Runtime.Order、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol;
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Prism.Ioc;



namespace SecsGem.Runtime.Protocol.Registry
{
    /// <summary>
    /// SxFy 消息处理器注册表。
    /// 启动时扫描一次并缓存 [SxFy → Type] 字典，热路径 GetHandler 无反射查找。
    /// </summary>
    public class SecsMessageRegistry
    {
        private readonly IContainerProvider _containerProvider;

        #region 私有字段

        private volatile bool _isInitialized = false;
        private volatile bool _disposed = false;

        /// <summary>
        /// 指令名称 → 处理器类型 缓存（启动时构建一次）
        /// </summary>
        private volatile Dictionary<string, Type> _handlerTypeCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="containerProvider">容器提供者</param>
        public SecsMessageRegistry(IContainerProvider containerProvider)
        {
            _containerProvider = containerProvider;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 扫描程序集并缓存所有指令处理器，同时注册到IOC容器中
        /// </summary>
        public void AutoRegisterHandlers()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SecsMessageRegistry));
            if (_isInitialized) return;

            lock (this)
            {
                if (_isInitialized) return;

                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var types = assembly.GetTypes()
                        .Where(t => typeof(SecsOrderBase).IsAssignableFrom(t) && !t.IsAbstract)
                        .ToList();

                    var cache = new Dictionary<string, Type>(types.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (var type in types)
                    {
                        // 优先读取 SecsOrderAttribute，未标注时按类名 SxFy 规则兜底
                        var attr = type.GetCustomAttribute<SecsOrderAttribute>();
                        string name;
                        if (attr != null)
                        {
                            name = attr.Name;
                        }
                        else if (type.Name.StartsWith("S", StringComparison.Ordinal) && type.Name.Contains("F"))
                        {
                            name = type.Name;
                            Console.WriteLine($"警告: {type.Name} 未标注 [SecsOrder]，已按类名规则注册，请补齐特性");
                        }
                        else
                        {
                            continue;
                        }

                        cache[name] = type;

                        // 将类型注册到IOC容器中（单例 + 属性注入容器）
                        if (_containerProvider is IContainerRegistry registry)
                        {
                            registry.RegisterSingleton(type, provider =>
                            {
                                var instance = Activator.CreateInstance(type) as SecsOrderBase;
                                if (instance != null)
                                {
                                    var containerProperty = instance.GetType().GetProperty("ContainerProviderSetter");
                                    if (containerProperty != null && containerProperty.CanWrite)
                                    {
                                        containerProperty.SetValue(instance, provider);
                                    }
                                }
                                return instance;
                            });
                        }
                    }

                    _handlerTypeCache = cache;
                    _isInitialized = true;

                    Console.WriteLine($"SecsMessageRegistry 初始化完成，注册了 {cache.Count} 个处理器");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"初始化工厂失败: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 手动注册处理器
        /// </summary>
        /// <typeparam name="T">处理器类型</typeparam>
        public void RegisterHandler<T>() where T : SecsOrderBase
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SecsMessageRegistry));

            // 将类型注册到IOC容器中
            if (_containerProvider != null)
            {
                var registry = _containerProvider as IContainerRegistry;
                if (registry != null)
                {
                    // 使用工厂方法注册，确保属性注入
                    registry.RegisterSingleton<T>(provider =>
                    {
                        var instance = provider.Resolve<T>();
                        var containerProperty = instance.GetType().GetProperty("ContainerProviderSetter");
                        if (containerProperty != null && containerProperty.CanWrite)
                        {
                            containerProperty.SetValue(instance, provider);
                        }
                        return instance;
                    });
                }
            }
        }

        /// <summary>
        /// 获取处理器实例（从缓存字典 O(1) 查找，无反射）
        /// </summary>
        /// <param name="commandName">指令名称</param>
        /// <returns>处理器实例</returns>
        public SecsOrderBase GetHandler(string commandName)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SecsMessageRegistry));

            if (!_isInitialized)
            {
                AutoRegisterHandlers();
            }

            if (commandName != null && _handlerTypeCache.TryGetValue(commandName, out var type))
            {
                return _containerProvider?.Resolve(type) as SecsOrderBase;
            }

            return null;
        }

        /// <summary>
        /// 判断该 Stream 是否有已注册处理器，用于区分 S9F3 和 S9F5。
        /// </summary>
        /// <returns>是否支持该 Stream</returns>
        public bool IsStreamSupported(byte stream)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SecsMessageRegistry));
            if (!_isInitialized) AutoRegisterHandlers();
            return _handlerTypeCache.Keys.Any(name => name.StartsWith($"S{stream}F", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>获取所有已注册的处理器类型。</summary>
        public IEnumerable<Type> GetRegisteredHandlers()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SecsMessageRegistry));

            if (!_isInitialized)
            {
                AutoRegisterHandlers();
            }

            return _handlerTypeCache.Values.ToList();
        }

        /// <summary>
        /// 清空所有处理器
        /// </summary>
        public void ClearHandlers()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SecsMessageRegistry));

            _handlerTypeCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            _isInitialized = false;
        }

        #endregion
 

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // 清空所有处理器
                    ClearHandlers();

                    // 记录销毁信息
                    Console.WriteLine("SecsMessageRegistry 销毁");
                }
                catch
                {
                    // 释放时出错静默处理
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        #endregion
    }
}
