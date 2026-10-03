// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：IProductA、IProductB、ConcreteProductA1、ConcreteProductA2、ConcreteProductB1、ConcreteProductB2。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System。导入不代表所有类型都会在运行时加载。
using System;

namespace Extension.Patterns.Creation.Factory
{
    // 定义产品接口
    public interface IProductA
    {
        string Name { get; }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 UseFeatureA。
        /// </summary>
        void UseFeatureA();
    }

    /// <summary>
    /// IProductB 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public interface IProductB
    {
        string Name { get; }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 UseFeatureB。
        /// </summary>
        void UseFeatureB();
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Collaborate。
        /// </summary>
        void Collaborate(IProductA productA);
    }

    /// <summary>
    /// 具体产品A1 - 属于产品家族1
    /// </summary>
    public class ConcreteProductA1 : IProductA
    {
        public string Name => "产品A1";
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 UseFeatureA。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void UseFeatureA()
        {
            Console.WriteLine($"{Name} 使用功能A");
        }
    }

    /// <summary>
    /// 具体产品A2 - 属于产品家族2
    /// </summary>
    public class ConcreteProductA2 : IProductA
    {
        public string Name => "产品A2";
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 UseFeatureA。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void UseFeatureA()
        {
            Console.WriteLine($"{Name} 使用功能A");
        }
    }

    /// <summary>
    /// 具体产品B1 - 属于产品家族1
    /// </summary>
    public class ConcreteProductB1 : IProductB
    {
        public string Name => "产品B1";
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 UseFeatureB。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void UseFeatureB()
        {
            Console.WriteLine($"{Name} 使用功能B");
        }
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Collaborate。
        /// 正文中的关键协作调用包括 nameof、WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void Collaborate(IProductA productA)
        {
            // ⛔ 序 65（2026-09-18 修）：原来直接取 `productA.Name`，对 null **零防御** ⇒ 裸 NRE
            // （`NullReferenceException` 里既没有参数名、也没有"是哪个参数错了"的信息，
            // 排查时只能靠读源码反推）。补成参数校验，与 `IAdvancedProduct` 系列的校验风格一致。
            // ✅ 下游：`Collaborate` 在生产代码里**零调用点**（全仓只有测试调用它）。
            if (productA == null)
                throw new ArgumentNullException(nameof(productA), "协作对象不能为 null。");

            Console.WriteLine($"{Name} 与 {productA.Name} 协作");
        }
    }

    /// <summary>
    /// 具体产品B2 - 属于产品家族2
    /// </summary>
    public class ConcreteProductB2 : IProductB
    {
        public string Name => "产品B2";
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 UseFeatureB。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void UseFeatureB()
        {
            Console.WriteLine($"{Name} 使用功能B");
        }
        
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Collaborate。
        /// 正文中的关键协作调用包括 nameof、WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public void Collaborate(IProductA productA)
        {
            // ⛔ 序 65（2026-09-18 修）：原来直接取 `productA.Name`，对 null **零防御** ⇒ 裸 NRE
            // （`NullReferenceException` 里既没有参数名、也没有"是哪个参数错了"的信息，
            // 排查时只能靠读源码反推）。补成参数校验，与 `IAdvancedProduct` 系列的校验风格一致。
            // ✅ 下游：`Collaborate` 在生产代码里**零调用点**（全仓只有测试调用它）。
            if (productA == null)
                throw new ArgumentNullException(nameof(productA), "协作对象不能为 null。");

            Console.WriteLine($"{Name} 与 {productA.Name} 协作");
        }
    }
}