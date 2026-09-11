using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P6-2：谓词翻译器形状测试（纯内存，不连库）。
    /// 锁定 SQL 形状 + 参数化（无字面量拼接）+ 不可译回退。
    /// </summary>
    public class DapperPredicateTranslatorTests
    {
        public enum Level : short { Low = 1, High = 2 }

        public class Item
        {
            [Key] public long Id { get; set; }
            public int Age { get; set; }
            public string? Name { get; set; }
            [Column("flag_col")] public bool Flag { get; set; }
            public Level Level { get; set; }
            public DateTime? Stamp { get; set; }
        }

        private static (string Where, Dapper.DynamicParameters Pars) MustTranslate(
            System.Linq.Expressions.Expression<Func<Item, bool>> predicate)
        {
            Assert.True(
                DapperPredicateTranslator<Item>.TryTranslate(predicate, ESQLType.PostgreSQL, out var where, out var pars),
                "期望可翻译");
            return (where, pars);
        }

        [Fact]
        public void GreaterThan_ProducesParameterizedComparison()
        {
            var (where, pars) = MustTranslate(x => x.Age > 20);
            Assert.Equal("age > @p0", where);
            Assert.Equal(20, pars.Get<int>("p0"));
        }

        [Fact]
        public void FlippedComparison_SwapsOperator()
        {
            var (where, pars) = MustTranslate(x => 20 < x.Age);
            Assert.Equal("age > @p0", where);
            Assert.Equal(20, pars.Get<int>("p0"));
        }

        [Fact]
        public void AndOrNot_Compose()
        {
            var (where, _) = MustTranslate(x => x.Age >= 18 && (x.Name == "a" || !(x.Flag)));
            Assert.Equal("(age >= @p0 AND (name = @p1 OR NOT (flag_col = @p2)))", where);
        }

        [Fact]
        public void NullConstant_ProducesIsNull()
        {
            var (where, _) = MustTranslate(x => x.Name == null);
            Assert.Equal("name IS NULL", where);
        }

        [Fact]
        public void NotNull_ProducesIsNotNull()
        {
            var (where, _) = MustTranslate(x => x.Stamp != null);
            Assert.Equal("stamp IS NOT NULL", where);
        }

        [Fact]
        public void BoolMember_ProducesEqualityWithTrue()
        {
            var (where, pars) = MustTranslate(x => x.Flag);
            Assert.Equal("flag_col = @p0", where);
            Assert.True(pars.Get<bool>("p0"));
        }

        [Fact]
        public void ColumnAttribute_UsesExplicitName()
        {
            var (where, _) = MustTranslate(x => x.Flag == true);
            Assert.StartsWith("flag_col =", where);
        }

        [Fact]
        public void EnumValue_PassesUnderlyingNumber()
        {
            // 编译器把枚举 == 直接降为底层比较（右侧成 Int32 常量），翻译器原样参数化即可。
            var (where, pars) = MustTranslate(x => x.Level == Level.High);
            Assert.Equal("level = @p0", where);
            Assert.Equal(2, pars.Get<int>("p0"));
        }

        [Fact]
        public void CapturedEnumVariable_PassesAsInt()
        {
            // 闭包枚举同样被编译器降为底层比较，存 Int32（SMALLINT 列隐式转换，四库通用）。
            var level = Level.High;
            var (where, pars) = MustTranslate(x => x.Level == level);
            Assert.Equal("level = @p0", where);
            Assert.Equal(2, pars.Get<int>("p0"));
        }

        [Fact]
        public void ClosureVariable_EvaluatedAsParameter()
        {
            var min = 30;
            var (where, pars) = MustTranslate(x => x.Age > min);
            Assert.Equal("age > @p0", where);
            Assert.Equal(30, pars.Get<int>("p0"));
        }

        [Fact]
        public void StringMethodCall_FallsBack()
        {
            Assert.False(DapperPredicateTranslator<Item>.TryTranslate(
                x => x.Name!.Contains("a"), ESQLType.PostgreSQL, out _, out _));
        }

        [Fact]
        public void Arithmetic_FallsBack()
        {
            Assert.False(DapperPredicateTranslator<Item>.TryTranslate(
                x => x.Age + 1 > 20, ESQLType.PostgreSQL, out _, out _));
        }

        [Fact]
        public void MySql_QuotesWithBackticks()
        {
            Assert.True(DapperPredicateTranslator<Item>.TryTranslate(
                x => x.Age > 1, ESQLType.MySQL, out var where, out _));
            Assert.Equal("`age` > @p0", where);
        }

        [Fact]
        public void SqlServer_QuotesWithBrackets()
        {
            Assert.True(DapperPredicateTranslator<Item>.TryTranslate(
                x => x.Age > 1, ESQLType.SQL_Server, out var where, out _));
            Assert.Equal("[age] > @p0", where);
        }
    }
}
