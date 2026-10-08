// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SplitDatePrunerTests、DayItem、MonthItem、YearItem。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition、System.ComponentModel.DataAnnotations。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P8：日期裁剪分析器测试（纯内存，不连库）。
    /// </summary>
    public class SplitDatePrunerTests
    {
        /// <summary>
        /// DayItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Day)]
        public class DayItem
        {
            [Key] public long Id { get; set; }
            public int Age { get; set; }
            [DbSplitField] public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// MonthItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Month)]
        public class MonthItem
        {
            [Key] public long Id { get; set; }
            [DbSplitField] public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// YearItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Year)]
        public class YearItem
        {
            [Key] public long Id { get; set; }
            [DbSplitField] public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// 测试相关入口 Day；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FindSplitFieldProperty、CandidateSuffixes；具体条件和先后次序见方法体。
        /// </summary>
        private static HashSet<string>? Day<T>(System.Linq.Expressions.Expression<Func<T, bool>> pred)
        {
            var prop = SplitDatePruner.FindSplitFieldProperty(typeof(T))!;
            return SplitDatePruner.CandidateSuffixes(pred, prop, SplitPeriod.Day);
        }

        /// <summary>
        /// 测试相关入口 Equality_ReturnsSingleSuffix；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Equality_ReturnsSingleSuffix()
        {
            var date = new DateTime(2026, 9, 10, 15, 30, 0);
            var result = Day<DayItem>(x => x.CreateTime == date);
            Assert.Equal(["20260910"], result);
        }

        /// <summary>
        /// 测试相关入口 ContradictoryEqualities_ReturnsEmpty；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、NotNull、Empty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ContradictoryEqualities_ReturnsEmpty()
        {
            var result = Day<DayItem>(x => x.CreateTime == new DateTime(2026, 9, 10) && x.CreateTime == new DateTime(2026, 9, 11));
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// 测试相关入口 BoundedRange_EnumeratesDays；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、Equal、Order；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BoundedRange_EnumeratesDays()
        {
            var result = Day<DayItem>(x => x.CreateTime >= new DateTime(2026, 9, 10) && x.CreateTime <= new DateTime(2026, 9, 12));
            Assert.Equal(["20260910", "20260911", "20260912"], result!.Order());
        }

        /// <summary>
        /// 测试相关入口 MixedStrictBounds_StillSound；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、Equal、Order；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void MixedStrictBounds_StillSound()
        {
            // >d1 AND <d3：首尾天必含（天内仍有满足时刻），枚举含边界。
            var result = Day<DayItem>(x => x.CreateTime > new DateTime(2026, 9, 10) && x.CreateTime < new DateTime(2026, 9, 12));
            Assert.Equal(["20260910", "20260911", "20260912"], result!.Order());
        }

        /// <summary>
        /// 测试相关入口 HalfBoundedRange_ReturnsNull；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Null、Day；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void HalfBoundedRange_ReturnsNull()
        {
            Assert.Null(Day<DayItem>(x => x.CreateTime >= new DateTime(2026, 9, 10)));
            Assert.Null(Day<DayItem>(x => x.CreateTime < new DateTime(2026, 9, 10)));
        }

        /// <summary>
        /// 测试相关入口 InvertedRange_ReturnsEmpty；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、NotNull、Empty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void InvertedRange_ReturnsEmpty()
        {
            var result = Day<DayItem>(x => x.CreateTime >= new DateTime(2026, 9, 12) && x.CreateTime <= new DateTime(2026, 9, 10));
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// 测试相关入口 NonDatePredicate_ReturnsNull；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Null、Day；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void NonDatePredicate_ReturnsNull()
        {
            Assert.Null(Day<DayItem>(x => x.Age > 20));
        }

        /// <summary>
        /// 测试相关入口 OrPredicate_ReturnsNull；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Null、Day；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void OrPredicate_ReturnsNull()
        {
            Assert.Null(Day<DayItem>(x => x.CreateTime == new DateTime(2026, 9, 10) || x.Age > 1));
        }

        /// <summary>
        /// 测试相关入口 OverCapRange_ReturnsNull；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void OverCapRange_ReturnsNull()
        {
            var result = Day<DayItem>(x => x.CreateTime >= new DateTime(2020, 1, 1) && x.CreateTime <= new DateTime(2026, 9, 10));
            Assert.Null(result);
        }

        /// <summary>
        /// 测试相关入口 MonthPeriod_EnumeratesMonths；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FindSplitFieldProperty、CandidateSuffixes、Equal、Order；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void MonthPeriod_EnumeratesMonths()
        {
            var prop = SplitDatePruner.FindSplitFieldProperty(typeof(MonthItem))!;
            var result = SplitDatePruner.CandidateSuffixes<MonthItem>(
                x => x.CreateTime >= new DateTime(2026, 7, 15) && x.CreateTime <= new DateTime(2026, 9, 5),
                prop, SplitPeriod.Month);
            Assert.Equal(["202607", "202608", "202609"], result!.Order());
        }

        /// <summary>
        /// 测试相关入口 YearPeriod_EnumeratesYears；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FindSplitFieldProperty、CandidateSuffixes、Equal、Order；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void YearPeriod_EnumeratesYears()
        {
            var prop = SplitDatePruner.FindSplitFieldProperty(typeof(YearItem))!;
            var result = SplitDatePruner.CandidateSuffixes<YearItem>(
                x => x.CreateTime >= new DateTime(2024, 6, 1) && x.CreateTime <= new DateTime(2026, 1, 1),
                prop, SplitPeriod.Year);
            Assert.Equal(["2024", "2025", "2026"], result!.Order());
        }

        /// <summary>
        /// 测试相关入口 EqualityOutsideRange_ReturnsEmpty；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、NotNull、Empty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EqualityOutsideRange_ReturnsEmpty()
        {
            var result = Day<DayItem>(x => x.CreateTime == new DateTime(2026, 9, 20)
                && x.CreateTime >= new DateTime(2026, 9, 10) && x.CreateTime <= new DateTime(2026, 9, 12));
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// 测试相关入口 FlippedBounds_PruneCorrectly；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、Equal、Order；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FlippedBounds_PruneCorrectly()
        {
            // 常量在左：c1 <= x && x < c2，与常规方向等价；上界日期恒含（inclusive 枚举，sound）。
            var result = Day<DayItem>(x => new DateTime(2026, 9, 10) <= x.CreateTime && x.CreateTime < new DateTime(2026, 9, 13));
            Assert.Equal(["20260910", "20260911", "20260912", "20260913"], result!.Order());
        }

        /// <summary>
        /// 测试相关入口 OrNestedInAnd_RangeStillApplies；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Day、Equal、Order；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void OrNestedInAnd_RangeStillApplies()
        {
            // OR 内部分支不下钻（保守），但同级范围约束仍生效。
            var result = Day<DayItem>(x => (x.CreateTime == new DateTime(2026, 9, 11) || x.Age > 100)
                && x.CreateTime >= new DateTime(2026, 9, 10) && x.CreateTime <= new DateTime(2026, 9, 12));
            Assert.Equal(["20260910", "20260911", "20260912"], result!.Order());
        }

        /// <summary>
        /// 测试相关入口 NullEquality_DoesNotPrune；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Null、Day；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void NullEquality_DoesNotPrune()
        {
            // 分表字段 == null 无法定表（闭包变量写法避开 CS8073 常量折叠警告，树形等价）。
            DateTime? noDate = null;
            Assert.Null(Day<DayItem>(x => x.CreateTime == noDate));
        }
    }
}
