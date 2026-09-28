using DataManager.SQL;
using System.Reflection.PortableExecutable;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P9-1：DataManager 依赖与平台架构守卫。
    /// P9 结论：Prism / Extension 均为框架基础必需库，DataManager 直接引用（不做隔离层、不做移除审计）；
    /// 同时禁止本阶段新增 x64 或新的宿主反向依赖（DataManager 不得反向引用宿主工程）。
    /// </summary>
    public class SqlDependencyArchitectureTests
    {
        /// <summary>宿主工程名前缀：DataManager 作为基础库不得反向引用它们。</summary>
        private static readonly string[] HostProjectPrefixes =
        {
            "WPFControl", "Packing", "Device", "FlowNodes",
            "DataManager.Workbench", "DataManager.Controls",
        };

        /// <summary>Prism 是基础必需库：模块类型直接实现 Prism 的 IModule 契约。</summary>
        [Fact]
        public void Prism_RemainsDirectlyReferenced()
        {
            Assert.Contains(typeof(Prism.Modularity.IModule), typeof(DataManagerModule).GetInterfaces());
        }

        /// <summary>Extension 是基础必需库：其类型可被 DataManager 侧解析（编译期直接引用）。</summary>
        [Fact]
        public void Extension_RemainsDirectlyReferenced()
        {
            Assert.Equal("Extension", typeof(Extension.UI.Theme.ThemeService).Assembly.GetName().Name);
        }

        /// <summary>
        /// 禁止新增 x64：DataManager / Extension 程序集保持 AnyCPU（ILONLY，且非 Amd64/IA64/Requires32Bit）。
        /// 用 PE 头判定而非 AssemblyName.ProcessorArchitecture（后者在 .NET 9 已过时且不再受支持）。
        /// </summary>
        [Theory]
        [InlineData(typeof(SQLManager))]
        [InlineData(typeof(DataManagerModule))]
        [InlineData(typeof(Extension.UI.Theme.ThemeService))]
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
