// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：EFCoreSplitTableTests、PlainEntity、DapperDayEntity、SharedMonthEntity、SharedYearEntity。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition、DataManager.Runtime.SQL.EFCore、Microsoft.EntityFrameworkCore。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Runtime.SQL.EFCore;
using Microsoft.EntityFrameworkCore;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// EFCore 分表对齐测试（仅验证模型映射与命名路由，无需真实数据库）。
    /// </summary>
    public class EFCoreSplitTableTests
    {
        /// <summary>
        /// PlainEntity 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class PlainEntity
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// DapperDayEntity 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(SplitType.Day)]
        public class DapperDayEntity
        {
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// SharedMonthEntity 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Month)]
        public class SharedMonthEntity
        {
            public long Id { get; set; }
            [DbSplitField]
            public DateTime? EventTime { get; set; }
        }

        /// <summary>
        /// SharedYearEntity 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Year)]
        public class SharedYearEntity
        {
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// 验证 HelperOf 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
        /// </summary>
        private static EFCoreHelper<T> HelperOf<T>() where T : class
        {
            return EFCoreHelper<T>.Create(new SQLParam
            {
                Name = "split-test",
                ServerIP = "127.0.0.1",
                Port = "5432",
                UserName = "u",
                Password = "p",
                DBName = "db",
                DBType = ESQLType.PostgreSQL,
                ToolType = ESQLToolType.EFCore,
            });
        }

        /// <summary>
        /// 验证 TableOf 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GetTableName、FindEntityType；具体条件和先后次序见方法体。
        /// </summary>
        private static string TableOf<T>(EFCoreBase<T> helper) where T : class
        {
            return helper.Model.FindEntityType(typeof(T))!.GetTableName()!;
        }

        /// <summary>
        /// 验证 PlainEntity_IsNotSplit_DefaultTableIsClassName 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 HelperOf、False、IsSplitTableEntity、Null、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void PlainEntity_IsNotSplit_DefaultTableIsClassName()
        {
            using var helper = HelperOf<PlainEntity>();

            Assert.False(helper.IsSplitTableEntity());
            Assert.Null(helper.SplitTableName);
            Assert.Equal(nameof(PlainEntity), TableOf(helper));
        }

        /// <summary>
        /// 验证 DapperAttribute_DaySuffix 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 HelperOf、True、IsSplitTableEntity、Equal、GetSplitTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData(2026, 3, 5, "DapperDayEntity_20260305")]
        public void DapperAttribute_DaySuffix(int y, int m, int d, string expected)
        {
            using var helper = HelperOf<DapperDayEntity>();

            Assert.True(helper.IsSplitTableEntity());
            Assert.Equal(expected, helper.GetSplitTableName(new DateTime(y, m, d)));
        }

        /// <summary>
        /// 验证 SharedAttribute_MonthSuffix_AndSplitField 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 HelperOf、True、IsSplitTableEntity、Equal、GetSplitTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SharedAttribute_MonthSuffix_AndSplitField()
        {
            using var helper = HelperOf<SharedMonthEntity>();

            Assert.True(helper.IsSplitTableEntity());
            Assert.Equal("SharedMonthEntity_202603", helper.GetSplitTableName(new DateTime(2026, 3, 5)));
        }

        /// <summary>
        /// 验证 SharedAttribute_YearSuffix_FallsBackToFirstDateTimeField 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 HelperOf、Equal、GetSplitTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SharedAttribute_YearSuffix_FallsBackToFirstDateTimeField()
        {
            using var helper = HelperOf<SharedYearEntity>();

            Assert.Equal("SharedYearEntity_2026", helper.GetSplitTableName(new DateTime(2026, 3, 5)));
        }

        /// <summary>
        /// 验证 Fork_MapsToSplitTable_AndDefaultModelUnaffected 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 HelperOf、ForkForTable、Equal、TableOf、nameof；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Fork_MapsToSplitTable_AndDefaultModelUnaffected()
        {
            using var helper = HelperOf<DapperDayEntity>();
            using var fork = helper.ForkForTable("DapperDayEntity_20260305");

            Assert.Equal("DapperDayEntity_20260305", fork.SplitTableName);
            Assert.Equal("DapperDayEntity_20260305", TableOf(fork));
            // 默认实例模型不受分表实例影响（缓存键隔离的证明）
            Assert.Equal(nameof(DapperDayEntity), TableOf(helper));
        }

        /// <summary>
        /// 验证 Fork_SameTable_SharesModel 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 HelperOf、ForkForTable、Same；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Fork_SameTable_SharesModel()
        {
            using var helper = HelperOf<DapperDayEntity>();
            using var fork1 = helper.ForkForTable("DapperDayEntity_20260305");
            using var fork2 = helper.ForkForTable("DapperDayEntity_20260305");

            Assert.Same(fork1.Model, fork2.Model);
        }
    }
}
