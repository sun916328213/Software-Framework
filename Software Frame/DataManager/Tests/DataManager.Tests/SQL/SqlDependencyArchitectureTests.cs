// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlDependencyArchitectureTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、System.Reflection.PortableExecutable。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using System.Reflection.PortableExecutable;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P9-1：DataManager 依赖与平台架构守卫。
    /// S5：基础类型由 Extension.Core 提供，UI 能力仅在 Controls 引用 Extension；
    /// 同时禁止本阶段新增 x64 或新的宿主反向依赖（DataManager 不得反向引用宿主工程）。
    /// </summary>
    public class SqlDependencyArchitectureTests
    {
        /// <summary>宿主工程名前缀：DataManager 作为基础库不得反向引用它们。</summary>
        private static readonly string[] HostProjectPrefixes =
        {
            "WPFControls", "Packing", "Device", "FlowNodes",
            "DataManager.Workbench", "DataManager.Controls",
        };

        /// <summary>
        /// Prism 是基础必需库（IModule/RegisterTypes 契约来源）。
        /// 模块实现已归属 DataManager.Controls，模块身份验证随之迁入 WPF 测试线
        /// （DataManager.Workbench.Tests.ModuleRegistrationTests）；逻辑线守住契约来源不变。
        /// </summary>
        [Fact]
        public void Prism_RemainsDirectlyReferenced()
        {
            Assert.Equal("Prism", typeof(Prism.Modularity.IModule).Assembly.GetName().Name);
        }

        /// <summary>逻辑线直接引用轻量基础类型。</summary>
        [Fact]
        public void ExtensionCore_RemainsDirectlyReferenced()
        {
            Assert.Equal("Extension.Core", typeof(Extension.Core.Collections.ConcurrentList<>).Assembly.GetName().Name);
        }

        /// <summary>
        /// 禁止新增 x64：DataManager / Extension 程序集保持 AnyCPU（ILONLY，且非 Amd64/IA64/Requires32Bit）。
        /// 用 PE 头判定而非 AssemblyName.ProcessorArchitecture（后者在 .NET 9 已过时且不再受支持）。
        /// </summary>
        [Theory]
        [InlineData(typeof(SQLManager))]
        [InlineData(typeof(Extension.Core.Collections.ConcurrentList<>))]
        public void Assemblies_StayAnyCpu_NotX64(Type type)
        {
            using var stream = File.OpenRead(type.Assembly.Location);
            using var pe = new PEReader(stream);
            var machine = pe.PEHeaders.CoffHeader.Machine;
            var corFlags = pe.PEHeaders.CorHeader!.Flags;

            Assert.NotEqual(Machine.Amd64, machine);
            Assert.NotEqual(Machine.IA64, machine);
            Assert.True(corFlags.HasFlag(CorFlags.ILOnly), $"{type.Assembly.GetName().Name} 非 ILOnly");
            Assert.False(corFlags.HasFlag(CorFlags.Requires32Bit), $"{type.Assembly.GetName().Name} 被钉为 x86");
        }

        /// <summary>禁止新的宿主反向依赖：DataManager 程序集不得引用宿主工程。</summary>
        [Fact]
        public void DataManager_DoesNotReferenceHostProjects()
        {
            var referenced = typeof(SQLManager).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .ToList();

            foreach (var prefix in HostProjectPrefixes)
            {
                Assert.DoesNotContain(referenced,
                    n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
