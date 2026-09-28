using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DataManager.SQL.Definition
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

        private static IEnumerable<Expression> Conjuncts(Expression node)
        {
            while (node is BinaryExpression { NodeType: ExpressionType.AndAlso } and)
            {
                yield return and.Right;
                node = and.Left;
            }
            yield return node;
        }

        private static DateTime? MatchDateEquality(BinaryExpression eq, PropertyInfo splitProp)
        {
            var leftCol = IsColumnOf(eq.Left, splitProp);
            var rightCol = IsColumnOf(eq.Right, splitProp);
            if (leftCol == rightCol)
                return null;
            return EvaluateDate(leftCol ? eq.Right : eq.Left);
        }

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
