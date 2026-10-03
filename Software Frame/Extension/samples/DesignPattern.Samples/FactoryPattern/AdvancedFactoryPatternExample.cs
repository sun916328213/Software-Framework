// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：SampleProductA、SampleProductB、SampleFactoryA、SampleFactoryB、AdvancedFactoryPatternExample。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Threading.Tasks、Extension.Patterns.Creation.Factory、Extension.Patterns.Creation.Factory.Advanced。导入不代表所有类型都会在运行时加载。
using System;
using System.Threading.Tasks;
using Extension.Patterns.Creation.Factory;
using Extension.Patterns.Creation.Factory.Advanced;

namespace DesignPattern.Samples.DesignPattern.FactoryPattern.Examples
{
    // 示例产品A
    public class SampleProductA : AdvancedProductBase
    {
        /// <summary>
        /// 初始化 SampleProductA 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SampleProductA(string id, string name) : base(id, name, "SampleA")
        {
        }
        
        /// <summary>
        /// 执行当前入口的处理流程；对应入口为 Execute。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public override void Execute()
        {
            Console.WriteLine($"执行示例产品A: {Name} (ID: {Id})");
        }
    }
    
    // 示例产品B
    public class SampleProductB : AdvancedProductBase
    {
        /// <summary>
        /// 初始化 SampleProductB 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SampleProductB(string id, string name) : base(id, name, "SampleB")
        {
        }
        
        /// <summary>
        /// 执行当前入口的处理流程；对应入口为 Execute。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public override void Execute()
        {
            Console.WriteLine($"执行示例产品B: {Name} (ID: {Id})");
        }
    }
    
    // 示例工厂A
    public class SampleFactoryA : AdvancedFactoryBase
    {
        public override string FactoryName => "示例工厂A";
        
        /// <summary>
        /// 初始化 SampleFactoryA 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 RegisterProduct；具体条件和先后次序见方法体。
        /// </summary>
        public SampleFactoryA()
        {
            // 注册产品创建器
            RegisterProduct("ProductA1", () => new SampleProductA("A1", "产品A1"));
            RegisterProduct("ProductA2", () => new SampleProductA("A2", "产品A2"));
        }
    }
    
    // 示例工厂B
    public class SampleFactoryB : AdvancedFactoryBase
    {
        public override string FactoryName => "示例工厂B";
        
        /// <summary>
        /// 初始化 SampleFactoryB 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 RegisterProduct；具体条件和先后次序见方法体。
        /// </summary>
        public SampleFactoryB()
        {
            // 注册产品创建函数
            RegisterProduct("ProductB1", () => new SampleProductB("ProductB1", "产品B1"));
            RegisterProduct("ProductB2", () => new SampleProductB("ProductB2", "产品B2"));
        }
    }
    
    /// <summary>
    /// 高级工厂模式使用示例
    /// </summary>
    public class AdvancedFactoryPatternExample
    {
        /// <summary>
        /// 运行当前处理流程；对应入口为 RunExample。
        /// 正文中的关键协作调用包括 WriteLine、CreateProduct、Execute、RegisterFactory、CreateProductAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public static async Task RunExample()
        {
            Console.WriteLine("=== 高级工厂模式示例 ===\n");
            
            // 1. 使用单个工厂创建产品
            Console.WriteLine("1. 使用单个工厂创建产品:");
            var factoryA = new SampleFactoryA();
            var productA1 = factoryA.CreateProduct("ProductA1");
            var productA2 = factoryA.CreateProduct<SampleProductA>("ProductA2");
            
            productA1.Execute();
            productA2.Execute();
            Console.WriteLine();
            
            // 2. 使用工厂管理器管理多个工厂
            Console.WriteLine("2. 使用工厂管理器管理多个工厂:");
            var factoryManager = new FactoryManager();
            factoryManager.RegisterFactory("FactoryA", factoryA);
            factoryManager.RegisterFactory("FactoryB", new SampleFactoryB());
            
            var productFromManager = factoryManager.CreateProduct<SampleProductB>("FactoryB", "ProductB1");
            productFromManager.Execute();
            Console.WriteLine();
            
            // 3. 异步创建产品
            Console.WriteLine("3. 异步创建产品:");
            var asyncProduct = await factoryManager.CreateProductAsync("FactoryA", "ProductA1");
            asyncProduct.Execute();
            Console.WriteLine();
            
            // 4. 查看工厂信息
            Console.WriteLine("4. 查看工厂信息:");
            Console.WriteLine($"已注册的工厂: {string.Join(", ", factoryManager.GetRegisteredFactoryNames())}");
            Console.WriteLine($"工厂A已注册的产品: {string.Join(", ", factoryA.GetRegisteredProductIds())}");
            Console.WriteLine($"工厂A缓存的产品数量: {factoryA.CachedProductCount}");
            Console.WriteLine();
            
            // 5. 使用工厂构建器
            Console.WriteLine("5. 使用工厂构建器:");
            var factoryManager2 = FactoryBuilder.Create()
                .AddFactory("FactoryA", new SampleFactoryA())
                .AddFactory("FactoryB", new SampleFactoryB())
                .Build();
                
            var productFromBuilder = factoryManager2.CreateProduct("FactoryB", "ProductB2");
            productFromBuilder.Execute();
            Console.WriteLine();
            
            Console.WriteLine("=== 示例结束 ===");
        }
    }
}