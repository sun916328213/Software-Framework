using System;
using System.Threading;
using System.Threading.Tasks;

namespace Extension.DesignPattern.SingletonPattern
{
    /// <summary>
    /// 单例模式封装类
    /// 提供多种方式创建和管理单例实例，确保全局唯一实例的创建和访问
    /// 支持同步和异步初始化，以及带参数的构造函数
    /// </summary>
    public static class Singleton
    {
        /// <summary>
        /// 获取指定类型的单例实例
        /// 使用Lazy<T>实现线程安全的延迟初始化
        /// </summary>
        /// <typeparam name="T">单例类型，必须有无参构造函数</typeparam>
        /// <returns>单例实例</returns>
        public static T GetInstance<T>() where T : class, new()
        {
            return SingletonInstance<T>.Instance.Value;
        }

        /// <summary>
        /// 获取指定类型的单例实例，支持自定义初始化方法
        /// </summary>
        /// <typeparam name="T">单例类型</typeparam>
        /// <param name="factory">实例创建工厂方法</param>
        /// <returns>单例实例</returns>
        public static T GetInstance<T>(Func<T> factory) where T : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return SingletonInstanceWithFactory<T>.GetInstance(factory);
        }

        /// <summary>
        /// 异步获取指定类型的单例实例
        /// 支持异步初始化场景
        /// </summary>
        /// <typeparam name="T">单例类型</typeparam>
        /// <param name="factory">异步实例创建工厂方法</param>
        /// <returns>单例实例任务</returns>
        public static Task<T> GetInstanceAsync<T>(Func<Task<T>> factory) where T : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return SingletonInstanceAsync<T>.GetInstanceAsync(factory);
        }
    }

    /// <summary>
    /// 单例实例持有者
    /// 使用静态嵌套类实现延迟初始化
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    internal static class SingletonInstance<T> where T : class, new()
    {
        /// <summary>
        /// 单例实例
        /// 使用Lazy<T>确保线程安全和延迟初始化
        /// </summary>
        public static readonly Lazy<T> Instance = new Lazy<T>(() => new T(), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// 支持工厂方法的单例实例持有者
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    internal static class SingletonInstanceWithFactory<T> where T : class
    {
        private static readonly object _lock = new object();
        private static T _instance;

        /// <summary>
        /// 获取或创建单例实例
        /// </summary>
        /// <param name="factory">实例创建工厂方法</param>
        /// <returns>单例实例</returns>
        public static T GetInstance(Func<T> factory)
        {
            // 双重检查锁定模式确保线程安全和性能
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = factory();
                        if (_instance == null)
                            throw new InvalidOperationException("工厂方法返回了null实例");
                    }
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 异步单例实例持有者
    /// 处理异步初始化场景
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    internal static class SingletonInstanceAsync<T> where T : class
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static T _instance;
        private static bool _initialized = false;
        private static bool _initializationFailed = false;

        /// <summary>
        /// 异步获取或创建单例实例
        /// </summary>
        /// <param name="factory">异步实例创建工厂方法</param>
        /// <returns>单例实例任务</returns>
        public static async Task<T> GetInstanceAsync(Func<Task<T>> factory)
        {
            // 异步版本的双重检查锁定
            if (_initialized)
                return _instance;
                
            // 如果之前的初始化失败了，直接抛出异常
            if (_initializationFailed)
                throw new InvalidOperationException("之前的初始化尝试失败，不会重试");

            await _semaphore.WaitAsync();
            try
            {
                if (!_initialized)
                {
                    try
                    {
                        _instance = await factory();
                        if (_instance == null)
                            throw new InvalidOperationException("工厂方法返回了null实例");
                        _initialized = true;
                    }
                    catch
                    {
                        _initializationFailed = true;
                        throw;
                    }
                }
                return _instance;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    /// <summary>
    /// 单例基类
    /// 提供简单的继承方式实现单例模式
    /// </summary>
    /// <typeparam name="T">派生类类型</typeparam>
    public abstract class SingletonBase<T> where T : class, new()
    {
        private static readonly Lazy<T> _instance = new Lazy<T>(() => new T(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance => _instance.Value;

        /// <summary>
        /// 保护构造函数，防止外部实例化
        /// </summary>
        protected SingletonBase() { }
    }

    /// <summary>
    /// 线程安全的单例实现（双重检查锁定模式）
    /// 提供对单例生命周期的显式控制
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public sealed class ThreadSafeSingleton<T> where T : class, new()
    {
        private static volatile T _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 私有构造函数，防止外部实例化
        /// </summary>
        private ThreadSafeSingleton() { }

        /// <summary>
        /// 重置单例实例
        /// 仅用于测试场景
        /// </summary>
        public static void ResetInstance()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }
    }
}