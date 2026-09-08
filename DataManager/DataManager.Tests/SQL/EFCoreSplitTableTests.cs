using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using Microsoft.EntityFrameworkCore;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// EFCore 分表对齐测试（仅验证模型映射与命名路由，无需真实数据库）。
    /// </summary>
    public class EFCoreSplitTableTests
    {
        public class PlainEntity
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        [DapperSplitTable(SplitType.Day)]
        public class DapperDayEntity
        {
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
        }

        [DbSplitTable(SplitPeriod.Month)]
        public class SharedMonthEntity
        {
            public long Id { get; set; }
            [DbSplitField]
            public DateTime? EventTime { get; set; }
        }

        [DbSplitTable(SplitPeriod.Year)]
        public class SharedYearEntity
        {
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
        }

        private static EFCoreHelper<T> HelperOf<T>() where T : class
        {
            var h = EFCoreHelper<T>.Create();
            h.SQLParam = new SQLParam
            {
                Name = "split-test",
                ServerIP = "127.0.0.1",
                Port = "5432",
                UserName = "u",
                Password = "p",
                DBName = "db",
                DBType = ESQLType.PostgreSQL,
                ToolType = ESQLToolType.EFCore,
            };
            return h;
        }

        private static string TableOf<T>(EFCoreBase<T> helper) where T : class
        {
            return helper.Model.FindEntityType(typeof(T))!.GetTableName()!;
        }

        [Fact]
        public void PlainEntity_IsNotSplit_DefaultTableIsClassName()
        {
            using var helper = HelperOf<PlainEntity>();

            Assert.False(helper.IsSplitTableEntity());
            Assert.Null(helper.SplitTableName);
            Assert.Equal(nameof(PlainEntity), TableOf(helper));
        }

        [Theory]
        [InlineData(2026, 3, 5, "DapperDayEntity_20260305")]
        public void DapperAttribute_DaySuffix(int y, int m, int d, string expected)
        {
            using var helper = HelperOf<DapperDayEntity>();

            Assert.True(helper.IsSplitTableEntity());
            Assert.Equal(expected, helper.GetSplitTableName(new DateTime(y, m, d)));
        }

        [Fact]
        public void SharedAttribute_MonthSuffix_AndSplitField()
        {
            using var helper = HelperOf<SharedMonthEntity>();

            Assert.True(helper.IsSplitTableEntity());
            Assert.Equal("SharedMonthEntity_202603", helper.GetSplitTableName(new DateTime(2026, 3, 5)));
        }

        [Fact]
        public void SharedAttribute_YearSuffix_FallsBackToFirstDateTimeField()
        {
            using var helper = HelperOf<SharedYearEntity>();

            Assert.Equal("SharedYearEntity_2026", helper.GetSplitTableName(new DateTime(2026, 3, 5)));
        }

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
