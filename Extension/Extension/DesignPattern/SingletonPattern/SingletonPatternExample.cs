using System;
using System.Threading.Tasks;
using Extension.DesignPattern;

namespace Extension.DesignPattern.SingletonPattern.Examples
{
    /// <summary>
    /// 示例服务类，用于演示单例模式的使用
    /// </summary>
    public class ExampleService
    {
        public string ServiceName { get; set; }
        
        public ExampleService()
        {
            ServiceName = "默认服务";
            Console.WriteLine("ExampleService 实例已创建");
        }
        
        public ExampleService(string serviceName)
        {
            ServiceName = serviceName;
            Console.WriteLine($"ExampleService 实例已创建，服务名称: {serviceName}");
        }
        
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

        public LoggerService()
        {
            Console.WriteLine("LoggerService 实例已创建");
        }
        
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
        
        public ConfigService()
        {
            Console.WriteLine("ConfigService 实例已创建");
        }
        
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