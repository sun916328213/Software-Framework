using Dapper;
using DataManager.SQL.Definition;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// P6-2：Dapper 分表谓词翻译器（v1 受限子集）。
    /// 支持：列与值的比较（==/!=/&gt;/&gt;=/&lt;/&lt;=，含 null→IS NULL/NOT NULL）、
    /// 列对列比较、&amp;&amp;/||/!、bool 列直写、Convert 解包、[Column] 显式名。
    /// 不支持（方法调用/算术/成员链等）一律回退内存过滤，调用方以 TryTranslate 返回值分支。
    /// 设计约束：全部值走参数（@p0…，四库统一前缀），不拼字面量，杜绝注入与字面量方言差异。
    /// </summary>
    internal static class DapperPredicateTranslator<T> where T : class
    {
        internal static bool TryTranslate(
            Expression<Func<T, bool>> predicate,
            ESQLType dbType,
            out string whereClause,
            out DynamicParameters parameters)
        {
            parameters = new DynamicParameters();
            whereClause = string.Empty;
            try
            {
                var visitor = new Translator(dbType, parameters);
                var sql = visitor.Translate(predicate.Body);
                if (sql == null)
                {
                    parameters = new DynamicParameters();
                    return false;
                }
                whereClause = sql;
                return true;
            }
            catch
            {
                parameters = new DynamicParameters();
                whereClause = string.Empty;
                return false;
            }
        }

        private sealed class Translator
        {
            private readonly ESQLType _dbType;
            private readonly DynamicParameters _pars;
            private int _seq;

            internal Translator(ESQLType dbType, DynamicParameters pars)
            {
                _dbType = dbType;
                _pars = pars;
            }

            internal string? Translate(Expression node) => node switch
            {
                BinaryExpression b => TranslateBinary(b),
                UnaryExpression { NodeType: ExpressionType.Not } n => TranslateNot(n.Operand),
                UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } c => Translate(ConvertStripped(c)),
                MemberExpression m when m.Type == typeof(bool) && TryGetColumn(m) is string col => Comparison(col, "=", true),
                _ => null,
            };

            private static Expression ConvertStripped(UnaryExpression c) => c.Operand;

            private string? TranslateNot(Expression operand)
            {
                var inner = Translate(operand);
                return inner == null ? null : $"NOT ({inner})";
            }

            private string? TranslateBinary(BinaryExpression b) => b.NodeType switch
            {
                ExpressionType.AndAlso => Combine(b, "AND"),
                ExpressionType.OrElse => Combine(b, "OR"),
                ExpressionType.Equal => TranslateComparison(b, "="),
                ExpressionType.NotEqual => TranslateComparison(b, "<>"),
                ExpressionType.GreaterThan => TranslateComparison(b, ">"),
                ExpressionType.GreaterThanOrEqual => TranslateComparison(b, ">="),
                ExpressionType.LessThan => TranslateComparison(b, "<"),
                ExpressionType.LessThanOrEqual => TranslateComparison(b, "<="),
                _ => null,
            };

            private string? Combine(BinaryExpression b, string op)
            {
                var left = Translate(b.Left);
                var right = Translate(b.Right);
                return left == null || right == null ? null : $"({left} {op} {right})";
            }

            private string? TranslateComparison(BinaryExpression b, string op)
            {
                var leftCol = TryGetColumn(b.Left);
                var rightCol = TryGetColumn(b.Right);

                if (leftCol != null && rightCol != null)
                    return $"{Quote(leftCol)} {op} {Quote(rightCol)}";

                string? column = leftCol ?? rightCol;
                if (column == null)
                    return null;

                // 值在另一侧：列在右时交换比较符。
                var valueSide = leftCol != null ? b.Right : b.Left;
                var effectiveOp = leftCol != null ? op : Flip(op);
                if (effectiveOp == null)
                    return null;

                object? value;
                try { value = Evaluate(valueSide); }
                catch { return null; }

                if (value == null)
                    return effectiveOp == "="
                        ? $"{Quote(column)} IS NULL"
                        : effectiveOp == "<>"
                            ? $"{Quote(column)} IS NOT NULL"
                            : null;

                return Comparison(column, effectiveOp, Normalize(value));
            }

            private string Comparison(string column, string op, object value)
            {
                var name = $"p{_seq++}";
                _pars.Add(name, value);
                return $"{Quote(column)} {op} @{name}";
            }

            private string Quote(string column) =>
                SqlNamingHelper.FormatColumnName(column, _dbType);

            private static string? Flip(string op) => op switch
            {
                ">" => "<",
                ">=" => "<=",
                "<" => ">",
                "<=" => ">=",
                "=" => "=",
                "<>" => "<>",
                _ => null,
            };

            /// <summary>列访问：参数成员（含 Convert 解包），返回属性名；否则 null。</summary>
            private static string? TryGetColumn(Expression node)            {
                while (node is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u)
                    node = u.Operand;
                if (node is MemberExpression m && m.Expression is ParameterExpression && m.Member is PropertyInfo prop)
                    return prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                return null;
            }

            private static object? Evaluate(Expression node)
            {
                if (node is ConstantExpression c)
                    return c.Value;
                // 闭包变量/表达式求值（不含参数，安全）。
                var lambda = Expression.Lambda(node);
                return lambda.Compile().DynamicInvoke();
            }

            private static object Normalize(object value)
            {
                // 枚举按底层数值传：各 provider 对未注册枚举映射行为不一，数值最稳。
                var type = value.GetType();
                if (type.IsEnum)
                    return Convert.ChangeType(value, Enum.GetUnderlyingType(type));
                return value;
            }
        }
    }
}
