using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace DataManager.SQL.EFCore
{
    /// <summary>
    /// EF Core 数据库帮助类
    /// 自动创建动态类型，确保每个实例有独立的模型缓存
    /// </summary>
    public class EFCoreHelper<T> : EFCoreBase<T> where T : class
    {
        // 保护构造函数，允许子类（动态类型）访问
        protected EFCoreHelper() { }

        // 静态计数器，用于生成唯一的类型名称
        private static int _typeCounter = 0;

        // 动态模块构建器
        private static readonly ModuleBuilder _moduleBuilder;

        static EFCoreHelper()
        {
            // 创建动态程序集和模块，使用链式调用简化代码
            _moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("EFCoreDynamicAssembly"),
                AssemblyBuilderAccess.Run)
                .DefineDynamicModule("EFCore");
        }

        /// <summary>
        /// 创建 EFCoreHelper 实例
        /// 每个实例都会有独立的动态类型，避免 EF Core 模型缓存冲突
        /// </summary>
        /// <returns>EFCoreHelper 实例</returns>
        public static EFCoreHelper<T> Create()
        {
            // 生成唯一的类型名称
            string typeName = $"EFCoreHelper_Dynamic_{Interlocked.Increment(ref _typeCounter)}_{Guid.NewGuid():N}";

            // 定义动态类型
            var typeBuilder = _moduleBuilder.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(EFCoreHelper<T>));

            // 创建类型
            var dynamicType = typeBuilder.CreateType();

            // 创建实例
            return (EFCoreHelper<T>)Activator.CreateInstance(dynamicType);
        }

        /// <summary>
        /// 创建 EFCoreHelper 实例并设置 SQLParam
        /// </summary>
        /// <param name="param">SQL 参数</param>
        /// <returns>EFCoreHelper 实例</returns>
        public static EFCoreHelper<T> Create(Definition.SQLParam param)
        {
            var instance = Create();
            instance.SQLParam = param;
            instance.Name = param.Name;
            return instance;
        }
    }
}
