// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DapperPredicateTranslatorTests、Level、Item。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition、System.ComponentModel.DataAnnotations、System.ComponentModel.DataAnnotations.Schema。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
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
        /// <summary>
        /// Level 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// 继承或实现的契约：short。具体约束按这些契约及各成员实现解释。
        /// </summary>
        public enum Level : short { Low = 1, High = 2 }

        /// <summary>
        /// Item 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class Item
        {
            [Key] public long Id { get; set; }
            public int Age { get; set; }
            public string? Name { get; set; }
            [Column("flag_col")] public bool Flag { get; set; }
            public Level Level { get; set; }
            public DateTime? Stamp { get; set; }
        }

        /// <summary>
        /// 测试相关入口 MustTranslate；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、TryTranslate；具体条件和先后次序见方法体。
        /// </summary>
        private static (string Where, Dapper.DynamicParameters Pars) MustTranslate(
            System.Linq.Expressions.Expression<Func<Item, bool>> predicate)
        {
            Assert.True(
                DapperPredicateTranslator<Item>.TryTranslate(predicate, ESQLType.PostgreSQL, out var where, out var pars),
                "期望可翻译");
            return (where, pars);
        }

        /// <summary>
        /// 测试相关入口 GreaterThan_ProducesParameterizedComparison；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal、Get；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GreaterThan_ProducesParameterizedComparison()
        {
            var (where, pars) = MustTranslate(x => x.Age > 20);
            Assert.Equal("age > @p0", where);
            Assert.Equal(20, pars.Get<int>("p0"));
        }

        /// <summary>
        /// 测试相关入口 FlippedComparison_SwapsOperator；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal、Get；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FlippedComparison_SwapsOperator()
        {
            var (where, pars) = MustTranslate(x => 20 < x.Age);
            Assert.Equal("age > @p0", where);
            Assert.Equal(20, pars.Get<int>("p0"));
        }

        /// <summary>
        /// 测试相关入口 AndOrNot_Compose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void AndOrNot_Compose()
        {
            var (where, _) = MustTranslate(x => x.Age >= 18 && (x.Name == "a" || !(x.Flag)));
            Assert.Equal("(age >= @p0 AND (name = @p1 OR NOT (flag_col = @p2)))", where);
        }

        /// <summary>
        /// 测试相关入口 NullConstant_ProducesIsNull；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void NullConstant_ProducesIsNull()
        {
            var (where, _) = MustTranslate(x => x.Name == null);
            Assert.Equal("name IS NULL", where);
        }

        /// <summary>
        /// 测试相关入口 NotNull_ProducesIsNotNull；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void NotNull_ProducesIsNotNull()
        {
            var (where, _) = MustTranslate(x => x.Stamp != null);
            Assert.Equal("stamp IS NOT NULL", where);
        }

        /// <summary>
        /// 测试相关入口 BoolMember_ProducesEqualityWithTrue；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal、True、Get；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BoolMember_ProducesEqualityWithTrue()
        {
            var (where, pars) = MustTranslate(x => x.Flag);
            Assert.Equal("flag_col = @p0", where);
            Assert.True(pars.Get<bool>("p0"));
        }

        /// <summary>
        /// 测试相关入口 ColumnAttribute_UsesExplicitName；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、StartsWith；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ColumnAttribute_UsesExplicitName()
        {
            var (where, _) = MustTranslate(x => x.Flag == true);
            Assert.StartsWith("flag_col =", where);
        }

        /// <summary>
        /// 测试相关入口 EnumValue_PassesUnderlyingNumber；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal、Get；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EnumValue_PassesUnderlyingNumber()
        {
            // 编译器把枚举 == 直接降为底层比较（右侧成 Int32 常量），翻译器原样参数化即可。
            var (where, pars) = MustTranslate(x => x.Level == Level.High);
            Assert.Equal("level = @p0", where);
            Assert.Equal(2, pars.Get<int>("p0"));
        }

        /// <summary>
        /// 测试相关入口 CapturedEnumVariable_PassesAsInt；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal、Get；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CapturedEnumVariable_PassesAsInt()
        {
            // 闭包枚举同样被编译器降为底层比较，存 Int32（SMALLINT 列隐式转换，四库通用）。
            var level = Level.High;
            var (where, pars) = MustTranslate(x => x.Level == level);
            Assert.Equal("level = @p0", where);
            Assert.Equal(2, pars.Get<int>("p0"));
        }

        /// <summary>
        /// 测试相关入口 ClosureVariable_EvaluatedAsParameter；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 MustTranslate、Equal、Get；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ClosureVariable_EvaluatedAsParameter()
        {
            var min = 30;
            var (where, pars) = MustTranslate(x => x.Age > min);
            Assert.Equal("age > @p0", where);
            Assert.Equal(30, pars.Get<int>("p0"));
        }

        /// <summary>
        /// 测试相关入口 StringMethodCall_FallsBack；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 False、TryTranslate、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void StringMethodCall_FallsBack()
        {
            Assert.False(DapperPredicateTranslator<Item>.TryTranslate(
                x => x.Name!.Contains("a"), ESQLType.PostgreSQL, out _, out _));
        }

        /// <summary>
        /// 测试相关入口 Arithmetic_FallsBack；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 False、TryTranslate；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Arithmetic_FallsBack()
        {
            Assert.False(DapperPredicateTranslator<Item>.TryTranslate(
                x => x.Age + 1 > 20, ESQLType.PostgreSQL, out _, out _));
        }

        /// <summary>
        /// 测试相关入口 MySql_QuotesWithBackticks；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、TryTranslate、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void MySql_QuotesWithBackticks()
        {
            Assert.True(DapperPredicateTranslator<Item>.TryTranslate(
                x => x.Age > 1, ESQLType.MySQL, out var where, out _));
            Assert.Equal("`age` > @p0", where);
        }

        /// <summary>
        /// 测试相关入口 SqlServer_QuotesWithBrackets；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、TryTranslate、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlServer_QuotesWithBrackets()
        {
            Assert.True(DapperPredicateTranslator<Item>.TryTranslate(
                x => x.Age > 1, ESQLType.SQL_Server, out var where, out _));
            Assert.Equal("[age] > @p0", where);
        }
    }
}
