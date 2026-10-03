// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ExampleService、LoggerService、ConfigService、SingletonPatternExample。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Threading.Tasks、Extension.Patterns.Creation.Singleton。导入不代表所有类型都会在运行时加载。
using System;
using System.Threading.Tasks;
using Extension.Patterns.Creation.Singleton;

namespace DesignPattern.Samples.DesignPattern.SingletonPattern.Examples
{
    /// <summary>
    /// 示例服务类，用于演示单例模式的使用
    /// </summary>
    public class ExampleService
    {
        public string ServiceName { get; set; }
        
        /// <summary>
        /// 初始化 ExampleService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public ExampleService()
        {
            ServiceName = "默认服务";
            Console.WriteLine("ExampleService 实例已创建");
        }
        
        /// <summary>
        /// 初始化 ExampleService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public ExampleService(string serviceName)
        {
            ServiceName = serviceName;
            Console.WriteLine($"ExampleService 实例已创建，服务名称: {serviceName}");
        }
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DoWork。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void DoWork()
        {
            Console.WriteLine($"{ServiceName} 正在工作...");
        }
    }
    
    /// <summary>
    /// 继承自SingletonBase的示例类
    /// </summary>
    public class LoggerService : SingletonBase<LoggerService>
    {
        private int _logCount = 0;

        /// <summary>
        /// 初始化 LoggerService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public LoggerService()
        {
            Console.WriteLine("LoggerService 实例已创建");
        }
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Log。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void Log(string message)
        {
            _logCount++;
            Console.WriteLine($"[{_logCount}] {message}");
        }
    }
    
    /// <summary>
    /// 使用ThreadSafeSingleton的示例类
    /// </summary>
    public class ConfigService
    {
        public string ConfigValue { get; set; } = "默认配置";
        
        /// <summary>
        /// 初始化 ConfigService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public ConfigService()
        {
            Console.WriteLine("ConfigService 实例已创建");
        }
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ShowConfig。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void ShowConfig()
        {
            Console.WriteLine($"当前配置: {ConfigValue}");
        }
    }
    
    /// <summary>
    /// 单例模式使用示例
    /// </summary>
    public class SingletonPatternExample
    {
        /// <summary>
        /// 运行当前处理流程；对应入口为 RunExample。
        /// 正文中的关键协作调用包括 WriteLine、GetInstance、DoWork、ReferenceEquals、Log；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public static async Task RunExample()
        {
            Console.WriteLine("=== 单例模式使用示例 ===\n");
            
            // 1. 使用默认构造函数创建单例
            Console.WriteLine("1. 使用默认构造函数创建单例:");
            var service1 = Singleton.GetInstance<ExampleService>();
            service1.DoWork();
            
            // 验证确实是同一个实例
            var service2 = Singleton.GetInstance<ExampleService>();
            Console.WriteLine($"service1 和 service2 是同一个实例: {ReferenceEquals(service1, service2)}\n");
            
            // 2. 使用工厂方法创建单例
            Console.WriteLine("2. 使用工厂方法创建单例:");
            var service3 = Singleton.GetInstance(() => new ExampleService("自定义服务"));
            service3.DoWork();
            Console.WriteLine();
            
            // 3. 使用继承方式创建单例
            Console.WriteLine("3. 使用继承方式创建单例:");
            LoggerService.Instance.Log("这是第一条日志");
            LoggerService.Instance.Log("这是第二条日志");
            Console.WriteLine();
            
            // 4. 使用ThreadSafeSingleton创建单例
            Console.WriteLine("4. 使用ThreadSafeSingleton创建单例:");
            var configService = ThreadSafeSingleton<ConfigService>.Instance;
            configService.ShowConfig();
            configService.ConfigValue = "修改后的配置";
            ThreadSafeSingleton<ConfigService>.Instance.ShowConfig();
            Console.WriteLine();
            
            // 5. 异步创建单例示例
            Console.WriteLine("5. 异步创建单例示例:");
            var asyncService = await Singleton.GetInstanceAsync(async () =>
            {
                // 模拟异步初始化过程
                await Task.Delay(100);
                return new ExampleService("异步创建的服务");
            });
            asyncService.DoWork();
            Console.WriteLine();
            
            Console.WriteLine("=== 示例结束 ===");
        }
    }
}