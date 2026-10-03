// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：DapperPredicateTranslator、Translator。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、Dapper、DataManager.Core.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace DataManager.Runtime.SQL.Dapper
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
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryTranslate。
        /// 正文中的关键协作调用包括 Translate；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// Translator 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
        /// </summary>
        private sealed class Translator
        {
            private readonly ESQLType _dbType;
            private readonly DynamicParameters _pars;
            private int _seq;

            /// <summary>
            /// 初始化 Translator 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
            /// </summary>
            internal Translator(ESQLType dbType, DynamicParameters pars)
            {
                _dbType = dbType;
                _pars = pars;
            }

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 Translate。
            /// 正文中的关键协作调用包括 TranslateBinary、TranslateNot、Translate、ConvertStripped、TryGetColumn；具体条件和先后次序见方法体。
            /// </summary>
            internal string? Translate(Expression node) => node switch
            {
                BinaryExpression b => TranslateBinary(b),
                UnaryExpression { NodeType: ExpressionType.Not } n => TranslateNot(n.Operand),
                UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } c => Translate(ConvertStripped(c)),
                MemberExpression m when m.Type == typeof(bool) && TryGetColumn(m) is string col => Comparison(col, "=", true),
                _ => null,
            };

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 ConvertStripped。
            /// </summary>
            private static Expression ConvertStripped(UnaryExpression c) => c.Operand;

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 TranslateNot。
            /// 正文中的关键协作调用包括 Translate；具体条件和先后次序见方法体。
            /// </summary>
            private string? TranslateNot(Expression operand)
            {
                var inner = Translate(operand);
                return inner == null ? null : $"NOT ({inner})";
            }

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 TranslateBinary。
            /// 正文中的关键协作调用包括 Combine、TranslateComparison；具体条件和先后次序见方法体。
            /// </summary>
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

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 Combine。
            /// 正文中的关键协作调用包括 Translate；具体条件和先后次序见方法体。
            /// </summary>
            private string? Combine(BinaryExpression b, string op)
            {
                var left = Translate(b.Left);
                var right = Translate(b.Right);
                return left == null || right == null ? null : $"({left} {op} {right})";
            }

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 TranslateComparison。
            /// 正文中的关键协作调用包括 TryGetColumn、Quote、Flip、Evaluate、Comparison；具体条件和先后次序见方法体。
            /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
            /// </summary>
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

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 Comparison。
            /// 正文中的关键协作调用包括 Add、Quote；具体条件和先后次序见方法体。
            /// </summary>
            private string Comparison(string column, string op, object value)
            {
                var name = $"p{_seq++}";
                _pars.Add(name, value);
                return $"{Quote(column)} {op} @{name}";
            }

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 Quote。
            /// 正文中的关键协作调用包括 FormatColumnName；具体条件和先后次序见方法体。
            /// </summary>
            private string Quote(string column) =>
                SqlNamingHelper.FormatColumnName(column, _dbType);

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 Flip。
            /// </summary>
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

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 Evaluate。
            /// 正文中的关键协作调用包括 Lambda、DynamicInvoke、Compile；具体条件和先后次序见方法体。
            /// </summary>
            private static object? Evaluate(Expression node)
            {
                if (node is ConstantExpression c)
                    return c.Value;
                // 闭包变量/表达式求值（不含参数，安全）。
                var lambda = Expression.Lambda(node);
                return lambda.Compile().DynamicInvoke();
            }

            /// <summary>
            /// 处理当前类型提供的操作；对应入口为 Normalize。
            /// 正文中的关键协作调用包括 GetType、ChangeType、GetUnderlyingType；具体条件和先后次序见方法体。
            /// </summary>
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
