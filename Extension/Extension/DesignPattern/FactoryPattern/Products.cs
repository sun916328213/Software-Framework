using System;

namespace Extension.DesignPattern.FactoryPattern
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
            Console.WriteLine($"{Name} 与 {productA.Name} 协作");
        }
    }
}