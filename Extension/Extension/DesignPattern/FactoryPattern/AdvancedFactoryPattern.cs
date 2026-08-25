using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Extension.DesignPattern.FactoryPattern
{
    /// <summary>
    /// 产品生命周期管理接口
    /// 支持产品的初始化和销毁
    /// </summary>
    public interface IProductLifecycle
    {
        /// <summary>
        /// 初始化产品
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// 销毁产品，释放资源
        /// </summary>
        void Dispose();
        
        /// <summary>
        /// 获取产品是否已初始化
        /// </summary>
        bool IsInitialized { get; }
    }
    
    /// <summary>
    /// 高级产品接口
    /// 所有可由工厂创建的产品都应该实现此接口
    /// </summary>
    public interface IAdvancedProduct : IDisposable
    {
        /// <summary>
        /// 产品ID
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// 产品名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 产品类型
        /// </summary>
        string Type { get; }
        
        /// <summary>
        /// 执行产品的默认操作
        /// </summary>
        void Execute();
    }
    
    /// <summary>
    /// 产品生命周期管理基类
    /// 提供IProductLifecycle接口的基本实现
    /// </summary>
    public abstract class ProductLifecycleBase : IProductLifecycle
    {
        private int _initialized = 0;
        private int _disposed = 0;
        
        /// <summary>
        /// 获取产品是否已初始化
        /// </summary>
        public bool IsInitialized => Interlocked.CompareExchange(ref _initialized, 0, 0) == 1;
        
        /// <summary>
        /// 获取产品是否已销毁
        /// </summary>
        public bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 0, 0) == 1;
        
        /// <summary>
        /// 初始化产品
        /// </summary>
        public void Initialize()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
            {
                OnInitialize();
            }
        }
        
        /// <summary>
        /// 销毁产品，释放资源
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                OnDispose();
            }
        }
        
        /// <summary>
        /// 初始化产品的具体实现
        /// 子类可以重写此方法以提供初始化逻辑
        /// </summary>
        protected virtual void OnInitialize() { }
        
        /// <summary>
        /// 销毁产品的具体实现
        /// 子类可以重写此方法以提供销毁逻辑
        /// </summary>
        protected virtual void OnDispose() { }
    }
    
    /// <summary>
    /// 缓存策略枚举
    /// </summary>
    public enum CacheStrategy
    {
        /// <summary>
        /// 不缓存产品实例
        /// </summary>
        None,
        /// <summary>
        /// 缓存所有产品实例
        /// </summary>
        All,
        /// <summary>
        /// 仅缓存单例产品实例
        /// </summary>
        Singleton
    }
    
    /// <summary>
    /// 高级工厂基类
    /// 提供通用的工厂功能，支持产品注册、创建和管理
    /// </summary>
    public abstract class AdvancedFactoryBase
    {
        #region 字段和属性
        
        /// <summary>
        /// 工厂名称
        /// </summary>
        public abstract string FactoryName { get; }
        
        /// <summary>
        /// 产品创建器字典（线程安全）
        /// </summary>
        protected readonly ConcurrentDictionary<string, Func<IAdvancedProduct>> _productFactories = 
            new ConcurrentDictionary<string, Func<IAdvancedProduct>>();
            
        /// <summary>
        /// 已创建的产品实例缓存（线程安全）
        /// </summary>
        protected readonly ConcurrentDictionary<string, IAdvancedProduct> _productCache = 
            new ConcurrentDictionary<string, IAdvancedProduct>();
        
        /// <summary>
        /// 缓存策略
        /// </summary>
        public CacheStrategy CacheStrategy { get; protected set; } = CacheStrategy.All;
        
        /// <summary>
        /// 产品是否线程安全
        /// </summary>
        public bool IsProductThreadSafe { get; protected set; } = false;
        
        #endregion
        
        #region 构造函数
        
        /// <summary>
        /// 构造函数
        /// </summary>
        protected AdvancedFactoryBase()
        {
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cacheStrategy">缓存策略</param>
        /// <param name="isProductThreadSafe">产品是否线程安全</param>
        protected AdvancedFactoryBase(CacheStrategy cacheStrategy, bool isProductThreadSafe = false)
        {
            CacheStrategy = cacheStrategy;
            IsProductThreadSafe = isProductThreadSafe;
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 注册产品创建器
        /// </summary>
        /// <param name="productId">产品ID</param>
        /// <param name="factoryFunc">产品创建函数</param>
        /// <exception cref="ArgumentException">当产品ID为空时抛出</exception>
        /// <exception cref="ArgumentNullException">当工厂函数为空时抛出</exception>
        public virtual void RegisterProduct(string productId, Func<IAdvancedProduct> factoryFunc)
        {
            if (string.IsNullOrEmpty(productId))
                throw new ArgumentException($"{nameof(productId)} 不能为空或空白", nameof(productId));
                
            if (factoryFunc == null)
                throw new ArgumentNullException(nameof(factoryFunc), $"{nameof(factoryFunc)} 不能为空");
                
            _productFactories[productId] = factoryFunc;
        }
        
        /// <summary>
        /// 注册产品类型（使用默认构造函数）
        /// </summary>
        /// <typeparam name="T">产品类型</typeparam>
        /// <param name="productId">产品ID</param>
        /// <exception cref="ArgumentException">当产品ID为空时抛出</exception>
        public virtual void RegisterProduct<T>(string productId) where T : IAdvancedProduct, new()
        {
            if (string.IsNullOrEmpty(productId))
                throw new ArgumentException($"{nameof(productId)} 不能为空或空白", nameof(productId));
                
            _productFactories[productId] = () => CreateProductInstance<T>();
        }
        
        /// <summary>
        /// 注册产品类型（使用自定义构造函数）
        /// </summary>
        /// <typeparam name="T">产品类型</typeparam>
        /// <param name="productId">产品ID</param>
        /// <param name="constructorFunc">自定义构造函数</param>
        /// <exception cref="ArgumentException">当产品ID为空时抛出</exception>
        /// <exception cref="ArgumentNullException">当构造函数为空时抛出</exception>
        public virtual void RegisterProduct<T>(string productId, Func<T> constructorFunc) where T : IAdvancedProduct
        {
            if (string.IsNullOrEmpty(productId))
                throw new ArgumentException($"{nameof(productId)} 不能为空或空白", nameof(productId));
                
            if (constructorFunc == null)
                throw new ArgumentNullException(nameof(constructorFunc), $"{nameof(constructorFunc)} 不能为空");
                
            _productFactories[productId] = () => constructorFunc();
        }
        
        /// <summary>
        /// 创建产品实例
        /// </summary>
        /// <param name="productId">产品ID</param>
        /// <returns>产品实例</returns>
        /// <exception cref="ArgumentException">当产品ID为空时抛出</exception>
        /// <exception cref="KeyNotFoundException">当未找到产品创建器时抛出</exception>
        public virtual IAdvancedProduct CreateProduct(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                throw new ArgumentException($"{nameof(productId)} 不能为空或空白", nameof(productId));
                
            // 首先尝试从缓存获取（如果缓存策略允许）
            if (CacheStrategy != CacheStrategy.None && _productCache.TryGetValue(productId, out var cachedProduct))
            {
                return cachedProduct;
            }
            
            // 尝试创建新产品
            if (_productFactories.TryGetValue(productId, out var factoryFunc))
            {
                var product = factoryFunc();
                if (product != null)
                {
                    // 初始化产品（如果实现了生命周期接口）
                    if (product is IProductLifecycle lifecycleProduct)
                    {
                        lifecycleProduct.Initialize();
                    }
                    
                    // 缓存产品实例（如果缓存策略允许）
                    if (CacheStrategy != CacheStrategy.None)
                    {
                        _productCache[productId] = product;
                    }
                    
                    return product;
                }
            }
            
            throw new KeyNotFoundException($"工厂 '{FactoryName}' 中未找到ID为 '{productId}' 的产品创建器");
        }
        
        /// <summary>
        /// 创建产品实例（泛型版本）
        /// </summary>
        /// <typeparam name="T">产品类型</typeparam>
        /// <param name="productId">产品ID</param>
        /// <returns>产品实例</returns>
        /// <exception cref="ArgumentException">当产品ID为空时抛出</exception>
        /// <exception cref="KeyNotFoundException">当未找到产品创建器时抛出</exception>
        /// <exception cref="InvalidCastException">当产品类型转换失败时抛出</exception>
        public virtual T CreateProduct<T>(string productId) where T : class, IAdvancedProduct
        {
            var product = CreateProduct(productId);
            if (product is T typedProduct)
            {
                return typedProduct;
            }
            
            throw new InvalidCastException($"无法将工厂 '{FactoryName}' 中的产品 '{productId}' 转换为类型 '{typeof(T).Name}'");
        }
        
        /// <summary>
        /// 异步创建产品实例
        /// </summary>
        /// <param name="productId">产品ID</param>
        /// <returns>产品实例任务</returns>
        public virtual async Task<IAdvancedProduct> CreateProductAsync(string productId)
        {
          return  await Task.Run(() => CreateProduct(productId));
        }
        
        /// <summary>
        /// 异步创建产品实例（泛型版本）
        /// </summary>
        /// <typeparam name="T">产品类型</typeparam>
        /// <param name="productId">产品ID</param>
        /// <returns>产品实例任务</returns>
        public virtual async Task<T> CreateProductAsync<T>(string productId) where T : class, IAdvancedProduct
        {
            return await Task.Run(() => CreateProduct<T>(productId));
        }
        
        /// <summary>
        /// 获取所有已注册的产品ID
        /// </summary>
        /// <returns>产品ID集合（只读）</returns>
        public virtual IReadOnlyCollection<string> GetRegisteredProductIds()
        {
            return _productFactories.Keys.ToList().AsReadOnly();
        }
        
        /// <summary>
        /// 检查是否存在指定ID的产品创建器
        /// </summary>
        /// <param name="productId">产品ID</param>
        /// <returns>如果存在返回true，否则返回false</returns>
        public virtual bool ContainsProduct(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return false;
                
            return _productFactories.ContainsKey(productId);
        }
        
        /// <summary>
        /// 移除产品创建器
        /// </summary>
        /// <param name="productId">产品ID</param>
        /// <returns>是否成功移除</returns>
        public virtual bool RemoveProduct(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return false;
                
            // 从缓存中移除并销毁产品
            if (_productCache.TryRemove(productId, out var cachedProduct))
            {
                cachedProduct.Dispose();
            }
            
            // 从工厂中移除
            return _productFactories.TryRemove(productId, out _);
        }
        
        /// <summary>
        /// 清空所有产品创建器和缓存
        /// </summary>
        public virtual void ClearProducts()
        {
            // 销毁所有缓存的产品
            foreach (var product in _productCache.Values)
            {
                product.Dispose();
            }
            
            _productFactories.Clear();
            _productCache.Clear();
        }
        
        /// <summary>
        /// 获取缓存中的产品实例数量
        /// </summary>
        public int CachedProductCount => _productCache.Count;
        
        /// <summary>
        /// 清空产品缓存
        /// </summary>
        public virtual void ClearCache()
        {
            // 销毁所有缓存的产品
            foreach (var product in _productCache.Values)
            {
                product.Dispose();
            }
            
            _productCache.Clear();
        }
        
        #endregion
        
        #region 私有辅助方法
        
        /// <summary>
        /// 创建产品实例（内部辅助方法）
        /// </summary>
        /// <typeparam name="T">产品类型</typeparam>
        /// <returns>产品实例</returns>
        private T CreateProductInstance<T>() where T : IAdvancedProduct, new()
        {
            return new T();
        }
        
        #endregion
    }
    
    /// <summary>
    /// 通用产品基类
    /// 提供IAdvancedProduct和IProductLifecycle接口的基本实现
    /// </summary>
    public abstract class AdvancedProductBase : ProductLifecycleBase, IAdvancedProduct
    {
        /// <summary>
        /// 产品ID
        /// </summary>
        public string Id { get; protected set; }
        
        /// <summary>
        /// 产品名称
        /// </summary>
        public string Name { get; protected set; }
        
        /// <summary>
        /// 产品类型
        /// </summary>
        public string Type { get; protected set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id">产品ID</param>
        /// <param name="name">产品名称</param>
        /// <param name="type">产品类型</param>
        protected AdvancedProductBase(string id, string name, string type)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id), $"{nameof(id)} 不能为空");
            Name = name ?? throw new ArgumentNullException(nameof(name), $"{nameof(name)} 不能为空");
            Type = type ?? throw new ArgumentNullException(nameof(type), $"{nameof(type)} 不能为空");
        }
        
        /// <summary>
        /// 执行产品的默认操作
        /// 子类可以重写此方法以提供具体实现
        /// </summary>
        public virtual void Execute()
        {
            Console.WriteLine($"执行产品 {Name} (ID: {Id}, Type: {Type}) 的默认操作");
        }
        
        /// <summary>
        /// 销毁产品，释放资源
        /// </summary>
        public new void Dispose()
        {
            base.Dispose();
        }
        
        /// <summary>
        /// 销毁产品的具体实现
        /// 子类可以重写此方法以提供销毁逻辑
        /// </summary>
        protected override void OnDispose()
        {
            // 释放产品资源
            Console.WriteLine($"销毁产品 {Name} (ID: {Id}, Type: {Type})");
        }
    }
    
    /// <summary>
    /// 工厂管理器
    /// 统一管理多个工厂实例
    /// </summary>
    public class FactoryManager
    {
        private readonly ConcurrentDictionary<string, AdvancedFactoryBase> _factories = 
            new ConcurrentDictionary<string, AdvancedFactoryBase>();
        
        /// <summary>
        /// 注册工厂实例
        /// </summary>
        /// <param name="factoryName">工厂名称</param>
        /// <param name="factory">工厂实例</param>
        /// <exception cref="ArgumentException">当工厂名称为空时抛出</exception>
        /// <exception cref="ArgumentNullException">当工厂实例为空时抛出</exception>
        public void RegisterFactory(string factoryName, AdvancedFactoryBase factory)
        {
            if (string.IsNullOrEmpty(factoryName))
                throw new ArgumentException($"{nameof(factoryName)} 不能为空或空白", nameof(factoryName));
                
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), $"{nameof(factory)} 不能为空");
                
            _factories[factoryName] = factory;
        }
        
        /// <summary>
        /// 获取工厂实例
        /// </summary>
        /// <param name="factoryName">工厂名称</param>
        /// <returns>工厂实例</returns>
        /// <exception cref="ArgumentException">当工厂名称为空时抛出</exception>
        /// <exception cref="KeyNotFoundException">当未找到工厂时抛出</exception>
        public AdvancedFactoryBase GetFactory(string factoryName)
        {
            if (string.IsNullOrEmpty(factoryName))
                throw new ArgumentException($"{nameof(factoryName)} 不能为空或空白", nameof(factoryName));
                
            if (_factories.TryGetValue(factoryName, out var factory))
            {
                return factory;
            }
            
            throw new KeyNotFoundException($"未找到名称为 '{factoryName}' 的工厂");
        }
        
        /// <summary>
        /// 获取工厂实例（泛型版本）
        /// </summary>
        /// <typeparam name="T">工厂类型</typeparam>
        /// <param name="factoryName">工厂名称</param>
        /// <returns>工厂实例</returns>
        /// <exception cref="ArgumentException">当工厂名称为空时抛出</exception>
        /// <exception cref="KeyNotFoundException">当未找到工厂时抛出</exception>
        /// <exception cref="InvalidCastException">当工厂类型转换失败时抛出</exception>
        public T GetFactory<T>(string factoryName) where T : AdvancedFactoryBase
        {
            var factory = GetFactory(factoryName);
            if (factory is T typedFactory)
            {
                return typedFactory;
            }
            
            throw new InvalidCastException($"无法将名称为 '{factoryName}' 的工厂转换为类型 '{typeof(T).Name}'");
        }
        
        /// <summary>
        /// 创建产品实例
        /// </summary>
        /// <param name="factoryName">工厂名称</param>
        /// <param name="productId">产品ID</param>
        /// <returns>产品实例</returns>
        /// <exception cref="ArgumentException">当工厂名称或产品ID为空时抛出</exception>
        /// <exception cref="KeyNotFoundException">当未找到工厂或产品创建器时抛出</exception>
        public IAdvancedProduct CreateProduct(string factoryName, string productId)
        {
            var factory = GetFactory(factoryName);
            return factory.CreateProduct(productId);
        }
        
        /// <summary>
        /// 创建产品实例（泛型版本）
        /// </summary>
        /// <typeparam name="T">产品类型</typeparam>
        /// <param name="factoryName">工厂名称</param>
        /// <param name="productId">产品ID</param>
        /// <returns>产品实例</returns>
        /// <exception cref="ArgumentException">当工厂名称或产品ID为空时抛出</exception>
        /// <exception cref="KeyNotFoundException">当未找到工厂或产品创建器时抛出</exception>
        /// <exception cref="InvalidCastException">当产品类型转换失败时抛出</exception>
        public T CreateProduct<T>(string factoryName, string productId) where T : class, IAdvancedProduct
        {
            var factory = GetFactory(factoryName);
            return factory.CreateProduct<T>(productId);
        }
        
        /// <summary>
        /// 异步创建产品实例
        /// </summary>
        /// <param name="factoryName">工厂名称</param>
        /// <param name="productId">产品ID</param>
        /// <returns>产品实例任务</returns>
        public async Task<IAdvancedProduct> CreateProductAsync(string factoryName, string productId)
        {
            return await Task.Run(() => CreateProduct(factoryName, productId));
        }
        
        /// <summary>
        /// 异步创建产品实例（泛型版本）
        /// </summary>
        /// <typeparam name="T">产品类型</typeparam>
        /// <param name="factoryName">工厂名称</param>
        /// <param name="productId">产品ID</param>
        /// <returns>产品实例任务</returns>
        public async Task<T> CreateProductAsync<T>(string factoryName, string productId) where T : class, IAdvancedProduct
        {
            return await Task.Run(() => CreateProduct<T>(factoryName, productId));
        }
        
        /// <summary>
        /// 获取所有已注册的工厂名称
        /// </summary>
        /// <returns>工厂名称集合（只读）</returns>
        public IReadOnlyCollection<string> GetRegisteredFactoryNames()
        {
            return _factories.Keys.ToList().AsReadOnly();
        }
        
        /// <summary>
        /// 检查是否存在指定名称的工厂
        /// </summary>
        /// <param name="factoryName">工厂名称</param>
        /// <returns>如果存在返回true，否则返回false</returns>
        public bool ContainsFactory(string factoryName)
        {
            if (string.IsNullOrEmpty(factoryName))
                return false;
                
            return _factories.ContainsKey(factoryName);
        }
        
        /// <summary>
        /// 移除工厂实例
        /// </summary>
        /// <param name="factoryName">工厂名称</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveFactory(string factoryName)
        {
            if (string.IsNullOrEmpty(factoryName))
                return false;
                
            if (_factories.TryRemove(factoryName, out var factory))
            {
                // 清空工厂中的所有产品
                factory.ClearProducts();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 清空所有工厂实例
        /// </summary>
        public void ClearFactories()
        {
            // 清空所有工厂中的产品
            foreach (var factory in _factories.Values)
            {
                factory.ClearProducts();
            }
            
            _factories.Clear();
        }
    }
    
    /// <summary>
    /// 工厂构建器
    /// 提供链式调用方式构建工厂管理器
    /// </summary>
    public class FactoryBuilder
    {
        private readonly FactoryManager _manager = new FactoryManager();
        
        /// <summary>
        /// 创建工厂构建器实例
        /// </summary>
        /// <returns>工厂构建器实例</returns>
        public static FactoryBuilder Create()
        {
            return new FactoryBuilder();
        }
        
        /// <summary>
        /// 添加工厂实例
        /// </summary>
        /// <param name="factoryName">工厂名称</param>
        /// <param name="factory">工厂实例</param>
        /// <returns>工厂构建器实例</returns>
        public FactoryBuilder AddFactory(string factoryName, AdvancedFactoryBase factory)
        {
            _manager.RegisterFactory(factoryName, factory);
            return this;
        }
        
        /// <summary>
        /// 添加工厂实例并配置缓存策略
        /// </summary>
        /// <param name="factoryName">工厂名称</param>
        /// <param name="factory">工厂实例</param>
        /// <param name="cacheStrategy">缓存策略</param>
        /// <returns>工厂构建器实例</returns>
        public FactoryBuilder AddFactory(string factoryName, AdvancedFactoryBase factory, CacheStrategy cacheStrategy)
        {
            _manager.RegisterFactory(factoryName, factory);
            // 通过反射设置缓存策略（如果需要）
            var cacheStrategyProperty = typeof(AdvancedFactoryBase).GetProperty(nameof(AdvancedFactoryBase.CacheStrategy));
            if (cacheStrategyProperty != null && cacheStrategyProperty.CanWrite)
            {
                cacheStrategyProperty.SetValue(factory, cacheStrategy);
            }
            return this;
        }
        
        /// <summary>
        /// 构建工厂管理器
        /// </summary>
        /// <returns>工厂管理器实例</returns>
        public FactoryManager Build()
        {
            return _manager;
        }
        
        /// <summary>
        /// 构建并返回工厂管理器（泛型版本）
        /// </summary>
        /// <returns>工厂管理器实例</returns>
        public T Build<T>() where T : FactoryManager
        {
            return _manager as T ?? throw new InvalidCastException($"无法将工厂管理器转换为类型 '{typeof(T).Name}'");
        }
    }
}