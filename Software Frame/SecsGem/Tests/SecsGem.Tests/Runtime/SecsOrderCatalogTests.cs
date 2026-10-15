// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SecsOrderCatalogTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls、System.Reflection、System.Text.RegularExpressions、SecsGem.Core.Order、SecsGem.Runtime.Order、SecsGem.Runtime。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls;

using System.Reflection;
using System.Text.RegularExpressions;
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol.Registry;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests
{
    /// <summary>
    /// Protocol/Messages 指令清单测试：锁定当前支持的指令，
    /// 重构（如工厂缓存化、门面化）时若意外丢失某个指令会立刻被发现。
    /// </summary>
    public class SecsOrderCatalogTests
    {
        private static readonly Regex OrderNamePattern = new(@"^S\d+F\d+$", RegexOptions.Compiled);

        /// <summary>
        /// 测试相关入口 GetOrderTypes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToList、Where、GetTypes、IsAssignableFrom；具体条件和先后次序见方法体。
        /// </summary>
        private static List<Type> GetOrderTypes()
        {
            var assembly = typeof(SecsOrderBase).Assembly;
            return assembly.GetTypes()
                .Where(t => t != typeof(SecsOrderBase)
                         && typeof(SecsOrderBase).IsAssignableFrom(t)
                         && !t.IsAbstract)
                .ToList();
        }

        /// <summary>
        /// 测试相关入口 Protocol_Registry_Is_A_Container_Singleton_And_Resolves_Handlers；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 RegisterTypes、Resolve、Same、AutoRegisterHandlers、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Protocol_Registry_Is_A_Container_Singleton_And_Resolves_Handlers()
        {
            var container = new TestPrismContainer();
            new SecsGemModule().RegisterTypes(container);

            var registry = (SecsMessageRegistry)container.Resolve(typeof(SecsMessageRegistry));
            Assert.Same(registry, container.Resolve(typeof(SecsMessageRegistry)));
            registry.AutoRegisterHandlers();
            Assert.Equal("S1F13", registry.GetHandler("S1F13")?.GetType().Name);
        }

        /// <summary>
        /// 测试相关入口 All_Order_Types_Follow_Naming_Convention；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToList、Where、Select、GetOrderTypes、IsMatch；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void All_Order_Types_Follow_Naming_Convention()
        {
            var badNames = GetOrderTypes()
                .Select(t => t.Name)
                .Where(n => !OrderNamePattern.IsMatch(n))
                .ToList();

            Assert.True(badNames.Count == 0,
                $"以下类名不符合 SxFy 规范: {string.Join(", ", badNames)}");
        }

        /// <summary>
        /// 测试相关入口 Core_Orders_Are_Present；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToHashSet、Select、GetOrderTypes、ToList、Where；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Core_Orders_Are_Present()
        {
            string[] expected =
            {
                // S1 设备状态与通信
                "S1F1", "S1F3", "S1F11", "S1F13", "S1F15", "S1F17", "S1F23",
                // S2 设备控制
                "S2F23", "S2F33", "S2F35", "S2F37", "S2F41",
                // S5 报警
                "S5F1", "S5F3", "S5F5", "S5F7",
                // S6 数据上报
                "S6F1", "S6F11",
                // S7 配方
                "S7F19", "S7F23", "S7F25",
                // S9 错误兜底 / S10 终端消息
                "S9F9", "S9F13",
                "S10F3", "S10F5",
            };

            var actual = GetOrderTypes().Select(t => t.Name).ToHashSet();

            var missing = expected.Where(e => !actual.Contains(e)).ToList();
            Assert.True(missing.Count == 0,
                $"缺少指令处理器: {string.Join(", ", missing)}");
        }

        /// <summary>
        /// 测试相关入口 All_Order_Types_Have_SecsOrderAttribute；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToList、Select、Where、GetOrderTypes、GetCustomAttribute；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void All_Order_Types_Have_SecsOrderAttribute()
        {
            var missing = GetOrderTypes()
                .Where(t => t.GetCustomAttribute<SecsOrderAttribute>() == null)
                .Select(t => t.Name)
                .ToList();

            Assert.True(missing.Count == 0,
                $"以下指令处理器缺少 [SecsOrder] 特性: {string.Join(", ", missing)}");
        }

        /// <summary>
        /// 测试相关入口 SecsOrderAttribute_Names_Match_ClassNames；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToList、Select、Where、GetOrderTypes、GetCustomAttribute；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SecsOrderAttribute_Names_Match_ClassNames()
        {
            var mismatched = GetOrderTypes()
                .Select(t => new
                {
                    Type = t,
                    Attr = t.GetCustomAttribute<SecsOrderAttribute>(),
                })
                .Where(x => x.Attr != null && x.Attr.Name != x.Type.Name)
                .Select(x => $"{x.Type.Name} -> {x.Attr.Name}")
                .ToList();

            Assert.True(mismatched.Count == 0,
                $"特性名称与类名不一致: {string.Join(", ", mismatched)}");
        }

        /// <summary>
        /// 测试相关入口 Every_Primary_Order_Has_Odd_Function；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToList、Select、Where、GetOrderTypes、Match；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Every_Primary_Order_Has_Odd_Function()
        {
            var wrong = GetOrderTypes()
                .Where(t =>
                {
                    var match = OrderNamePattern.Match(t.Name);
                    if (!match.Success) return false;
                    int f = int.Parse(match.Value.Substring(match.Value.IndexOf('F') + 1));
                    return f % 2 == 0; // 主消息(设备收/发请求) F 应为奇数，回复为偶数
                })
                .Select(t => t.Name)
                .ToList();

            Assert.True(wrong.Count == 0,
                $"以下主消息指令的 F 不是奇数: {string.Join(", ", wrong)}");
        }
    }
}
