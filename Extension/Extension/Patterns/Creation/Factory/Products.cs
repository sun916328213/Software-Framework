using System;

namespace Extension.Patterns.Creation.Factory
{
    // 定义产品接口
    public interface IProductA
    {
        string Name { get; }
        void UseFeatureA();
    }

    public interface IProductB
    {
        string Name { get; }
        void UseFeatureB();
        void Collaborate(IProductA productA);
    }

    /// <summary>
    /// 具体产品A1 - 属于产品家族1
    /// </summary>
    public class ConcreteProductA1 : IProductA
    {
        public string Name => "产品A1";
        
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
        
        public void UseFeatureB()
        {
            Console.WriteLine($"{Name} 使用功能B");
        }
        
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
        
        public void UseFeatureB()
        {
            Console.WriteLine($"{Name} 使用功能B");
        }
        
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