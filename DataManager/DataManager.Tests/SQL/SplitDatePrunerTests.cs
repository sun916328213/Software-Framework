using DataManager.SQL.Definition;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P8：日期裁剪分析器测试（纯内存，不连库）。
    /// </summary>
    public class SplitDatePrunerTests
    {
        [DbSplitTable(SplitPeriod.Day)]
        public class DayItem
        {
            [Key] public long Id { get; set; }
            public int Age { get; set; }
            [DbSplitField] public DateTime CreateTime { get; set; }
        }

        [DbSplitTable(SplitPeriod.Month)]
        public class MonthItem
        {
            [Key] public long Id { get; set; }
            [DbSplitField] public DateTime CreateTime { get; set; }
        }

        [DbSplitTable(SplitPeriod.Year)]
        public class YearItem
        {
            [Key] public long Id { get; set; }
            [DbSplitField] public DateTime CreateTime { get; set; }
        }

        private static HashSet<string>? Day<T>(System.Linq.Expressions.Expression<Func<T, bool>> pred)
        {
            var prop = SplitDatePruner.FindSplitFieldProperty(typeof(T))!;
            return SplitDatePruner.CandidateSuffixes(pred, prop, SplitPeriod.Day);
        }

        [Fact]
        public void Equality_ReturnsSingleSuffix()
        {
            var date = new DateTime(2026, 9, 10, 15, 30, 0);
            var result = Day<DayItem>(x => x.CreateTime == date);
            Assert.Equal(["20260910"], result);
        }

        [Fact]
        public void ContradictoryEqualities_ReturnsEmpty()
        {
            var result = Day<DayItem>(x => x.CreateTime == new DateTime(2026, 9, 10) && x.CreateTime == new DateTime(2026, 9, 11));
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void BoundedRange_EnumeratesDays()
        {
            var result = Day<DayItem>(x => x.CreateTime >= new DateTime(2026, 9, 10) && x.CreateTime <= new DateTime(2026, 9, 12));
            Assert.Equal(["20260910", "20260911", "20260912"], result!.Order());
        }

        [Fact]
        public void MixedStrictBounds_StillSound()
        {
            // >d1 AND <d3：首尾天必含（天内仍有满足时刻），枚举含边界。
            var result = Day<DayItem>(x => x.CreateTime > new DateTime(2026, 9, 10) && x.CreateTime < new DateTime(2026, 9, 12));
            Assert.Equal(["20260910", "20260911", "20260912"], result!.Order());
        }

        [Fact]
        public void HalfBoundedRange_ReturnsNull()
        {
            Assert.Null(Day<DayItem>(x => x.CreateTime >= new DateTime(2026, 9, 10)));
            Assert.Null(Day<DayItem>(x => x.CreateTime < new DateTime(2026, 9, 10)));
        }

        [Fact]
        public void InvertedRange_ReturnsEmpty()
        {
            var result = Day<DayItem>(x => x.CreateTime >= new DateTime(2026, 9, 12) && x.CreateTime <= new DateTime(2026, 9, 10));
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void NonDatePredicate_ReturnsNull()
        {
            Assert.Null(Day<DayItem>(x => x.Age > 20));
        }

        [Fact]
        public void OrPredicate_ReturnsNull()
        {
            Assert.Null(Day<DayItem>(x => x.CreateTime == new DateTime(2026, 9, 10) || x.Age > 1));
        }

        [Fact]
        public void OverCapRange_ReturnsNull()
        {
            var result = Day<DayItem>(x => x.CreateTime >= new DateTime(2020, 1, 1) && x.CreateTime <= new DateTime(2026, 9, 10));
            Assert.Null(result);
        }

        [Fact]
        public void MonthPeriod_EnumeratesMonths()
        {
            var prop = SplitDatePruner.FindSplitFieldProperty(typeof(MonthItem))!;
            var result = SplitDatePruner.CandidateSuffixes<MonthItem>(
                x => x.CreateTime >= new DateTime(2026, 7, 15) && x.CreateTime <= new DateTime(2026, 9, 5),
                prop, SplitPeriod.Month);
            Assert.Equal(["202607", "202608", "202609"], result!.Order());
        }

        [Fact]
        public void YearPeriod_EnumeratesYears()
        {
            var prop = SplitDatePruner.FindSplitFieldProperty(typeof(YearItem))!;
            var result = SplitDatePruner.CandidateSuffixes<YearItem>(
                x => x.CreateTime >= new DateTime(2024, 6, 1) && x.CreateTime <= new DateTime(2026, 1, 1),
                prop, SplitPeriod.Year);
            Assert.Equal(["2024", "2025", "2026"], result!.Order());
        }

        [Fact]
        public void EqualityOutsideRange_ReturnsEmpty()
        {
            var result = Day<DayItem>(x => x.CreateTime == new DateTime(2026, 9, 20)
                && x.CreateTime >= new DateTime(2026, 9, 10) && x.CreateTime <= new DateTime(2026, 9, 12));
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void FlippedBounds_PruneCorrectly()
        {
            // 常量在左：c1 <= x && x < c2，与常规方向等价；上界日期恒含（inclusive 枚举，sound）。
            var result = Day<DayItem>(x => new DateTime(2026, 9, 10) <= x.CreateTime && x.CreateTime < new DateTime(2026, 9, 13));
            Assert.Equal(["20260910", "20260911", "20260912", "20260913"], result!.Order());
        }

        [Fact]
        public void OrNestedInAnd_RangeStillApplies()
        {
            // OR 内部分支不下钻（保守），但同级范围约束仍生效。
            var result = Day<DayItem>(x => (x.CreateTime == new DateTime(2026, 9, 11) || x.Age > 100)
                && x.CreateTime >= new DateTime(2026, 9, 10) && x.CreateTime <= new DateTime(2026, 9, 12));
            Assert.Equal(["20260910", "20260911", "20260912"], result!.Order());
        }

        [Fact]
        public void NullEquality_DoesNotPrune()
        {
            // 分表字段 == null 无法定表（闭包变量写法避开 CS8073 常量折叠警告，树形等价）。
            DateTime? noDate = null;
            Assert.Null(Day<DayItem>(x => x.CreateTime == noDate));
        }
    }
}
