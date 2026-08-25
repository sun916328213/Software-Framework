using Secs.Order;
using Extension.DesignPattern.FactoryPattern;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Prism.Ioc;



namespace SemiTool.Tool
{
    /// <summary>
    /// Secs命令适配器 - 将SecsOrderBase转换为IAdvancedProduct接口
    /// </summary>
    public class SecsOrderAdapter : IAdvancedProduct
    {
        private readonly SecsOrderBase _secsOrder;

        public string Id { get; }
        public string Name { get; }
        public string Type => "Secs命令";

        public SecsOrderAdapter(SecsOrderBase secsOrder, string productId)
        {
            _secsOrder = secsOrder ?? throw new ArgumentNullException(nameof(secsOrder));
            Id = productId ?? throw new ArgumentNullException(nameof(productId));
            Name = secsOrder.GetType().Name;
        }

        public void Execute()
        {
            // Secs命令不需要执行默认操作
            Console.WriteLine($"Secs命令 {Name} 已创建");
        }

        /// <summary>
        /// 获取原始Secs命令实例
        /// </summary>
        public SecsOrderBase GetSecsOrder() => _secsOrder;
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 释放Secs命令资源
        }
    }

    /// <summary>
    /// 基于Extension工厂模式的Secs命令工厂。
    /// 启动时扫描一次并缓存 [SxFy → Type] 字典，热路径 GetHandler 为 O(1)，无反射开销。
    /// </summary>
    public class FactoryCollection
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
        public FactoryCollection(IContainerProvider containerProvider)
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
            if (_disposed) throw new ObjectDisposedException(nameof(FactoryCollection));
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

                    Console.WriteLine($"FactoryCollection 初始化完成，注册了 {cache.Count} 个处理器");
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
            if (_disposed) throw new ObjectDisposedException(nameof(FactoryCollection));

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
            if (_disposed) throw new ObjectDisposedException(nameof(FactoryCollection));

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
        /// 获取所有已注册的处理器类型
        /// </summary>
        /// <returns>处理器类型列表</returns>
        public IEnumerable<Type> GetRegisteredHandlers()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FactoryCollection));

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
            if (_disposed) throw new ObjectDisposedException(nameof(FactoryCollection));

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
                    Console.WriteLine($"FactoryCollection 销毁");
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