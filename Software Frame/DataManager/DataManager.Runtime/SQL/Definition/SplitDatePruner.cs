// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SplitDatePruner。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL、System。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DataManager.Runtime.SQL.Definition
{
    /// <summary>
    /// P8：分表日期约束分析（Dapper/EFCore 共用）。
    /// 从 AND 链提取分表字段的等值/范围约束，展开为候选后缀集：
    /// - 等值：精确后缀；多个不同日期 AND → 空集（无解）。
    /// - 范围（&gt;/&gt;=/&lt;/&lt;=）：按周期枚举槽位；单边无界或超上限（默认 500 槽）→ null（不裁剪）。
    /// - 无可用约束 → null；OR/方法调用等不分析（调用方全表，正确优先）。
    /// </summary>
    internal static class SplitDatePruner
    {
        internal const int MaxSlots = 500;

        /// <summary>分表字段属性：显式标记优先，否则首个可读写 DateTime（与路由取值同规则）。</summary>
        internal static PropertyInfo? FindSplitFieldProperty(Type entityType)
        {
            var props = entityType.GetProperties().Where(p => p.CanRead && p.CanWrite).ToArray();
            foreach (var prop in props)
            {
                if (prop.GetCustomAttributes(typeof(Dapper.DapperSplitFieldAttribute), false).Any()
                    || prop.GetCustomAttributes(typeof(DbSplitFieldAttribute), false).Any())
                    return prop;
            }
            return props.FirstOrDefault(p =>
            {
                var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                return t == typeof(DateTime);
            });
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CandidateSuffixes。
        /// 正文中的关键协作调用包括 Conjuncts、MatchDateEquality、Add、TryMatchBound、ToList；具体条件和先后次序见方法体。
        /// </summary>
        internal static HashSet<string>? CandidateSuffixes<T>(
            Expression<Func<T, bool>> predicate, PropertyInfo splitProp, SplitPeriod period, int cap = MaxSlots)
        {
            var equalities = new HashSet<DateTime>();
            DateTime? lower = null, upper = null;
            var found = false;

            foreach (var conjunct in Conjuncts(predicate.Body))
            {
                if (conjunct is not BinaryExpression b)
                    continue;
                if (b.NodeType == ExpressionType.Equal)
                {
                    var date = MatchDateEquality(b, splitProp);
                    if (date == null)
                        continue;
                    found = true;
                    equalities.Add(date.Value.Date);
                }
                else if (TryMatchBound(b, splitProp, out var isLower, out var bound))
                {
                    found = true;
                    if (isLower)
                        lower = lower == null || bound > lower ? bound : lower;
                    else
                        upper = upper == null || bound < upper ? bound : upper;
                }
            }

            if (!found)
                return null;

            if (equalities.Count > 1)
                return new HashSet<string>();

            if (equalities.Count > 0)
            {
                // 等值 + 范围组合：等值须落在范围内，否则无解。
                var kept = equalities.Where(d =>
                    (lower == null || d >= lower.Value.Date) &&
                    (upper == null || d <= upper.Value.Date)).ToList();
                return kept.Select(d => DbSplitPeriod.SuffixFor(period, d)).ToHashSet();
            }

            if (lower == null || upper == null)
                return null;
            if (lower.Value.Date > upper.Value.Date)
                return new HashSet<string>();

            return ExpandRange(period, lower.Value.Date, upper.Value.Date, cap);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ExpandRange。
        /// 正文中的关键协作调用包括 AddDays、Add、SuffixFor、AddMonths；具体条件和先后次序见方法体。
        /// </summary>
        private static HashSet<string>? ExpandRange(SplitPeriod period, DateTime from, DateTime to, int cap)
        {
            var result = new HashSet<string>();
            switch (period)
            {
                case SplitPeriod.Day:
                    if ((to - from).Days + 1 > cap)
                        return null;
                    for (var d = from; d <= to; d = d.AddDays(1))
                        result.Add(DbSplitPeriod.SuffixFor(period, d));
                    return result;
                case SplitPeriod.Month:
                    {
                        var count = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
                        if (count > cap)
                            return null;
                        var cursor = new DateTime(from.Year, from.Month, 1);
                        var end = new DateTime(to.Year, to.Month, 1);
                        while (cursor <= end)
                        {
                            result.Add(DbSplitPeriod.SuffixFor(period, cursor));
                            cursor = cursor.AddMonths(1);
                        }
                        return result;
                    }
                case SplitPeriod.Year:
                    {
                        var count = to.Year - from.Year + 1;
                        if (count > cap)
                            return null;
                        for (var y = from.Year; y <= to.Year; y++)
                            result.Add(DbSplitPeriod.SuffixFor(period, new DateTime(y, 1, 1)));
                        return result;
                    }
                default:
                    return null;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Conjuncts。
        /// </summary>
        private static IEnumerable<Expression> Conjuncts(Expression node)
        {
            while (node is BinaryExpression { NodeType: ExpressionType.AndAlso } and)
            {
                yield return and.Right;
                node = and.Left;
            }
            yield return node;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MatchDateEquality。
        /// 正文中的关键协作调用包括 IsColumnOf、EvaluateDate；具体条件和先后次序见方法体。
        /// </summary>
        private static DateTime? MatchDateEquality(BinaryExpression eq, PropertyInfo splitProp)
        {
            var leftCol = IsColumnOf(eq.Left, splitProp);
            var rightCol = IsColumnOf(eq.Right, splitProp);
            if (leftCol == rightCol)
                return null;
            return EvaluateDate(leftCol ? eq.Right : eq.Left);
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryMatchBound。
        /// 正文中的关键协作调用包括 IsColumnOf、EvaluateDate；具体条件和先后次序见方法体。
        /// </summary>
        private static bool TryMatchBound(BinaryExpression b, PropertyInfo splitProp, out bool isLower, out DateTime? bound)
        {
            isLower = false;
            bound = null;
            var leftCol = IsColumnOf(b.Left, splitProp);
            var rightCol = IsColumnOf(b.Right, splitProp);
            if (leftCol == rightCol)
                return false;

            // 列在左：x >= d 为下界；列在右则翻转。
            var lowerOp = leftCol
                ? b.NodeType is ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
                : b.NodeType is ExpressionType.LessThan or ExpressionType.LessThanOrEqual;
            var date = EvaluateDate(leftCol ? b.Right : b.Left);
            if (date == null)
                return false;
            isLower = lowerOp;
            bound = date.Value.Date;
            return true;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsColumnOf。
        /// </summary>
        private static bool IsColumnOf(Expression node, PropertyInfo splitProp)
        {
            while (node is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u)
                node = u.Operand;
            return node is MemberExpression m
                && m.Expression is ParameterExpression
                && m.Member is PropertyInfo prop
                && prop.Name == splitProp.Name
                && prop.DeclaringType == splitProp.DeclaringType;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EvaluateDate。
        /// 正文中的关键协作调用包括 DynamicInvoke、Compile、Lambda；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static DateTime? EvaluateDate(Expression node)
        {
            try
            {
                var value = node is ConstantExpression c
                    ? c.Value
                    : Expression.Lambda(node).Compile().DynamicInvoke();
                if (value is DateTime d)
                    return d;
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
