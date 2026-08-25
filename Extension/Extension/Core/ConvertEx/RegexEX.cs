using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Extension.Core.ConvertEx
{
    public static class RegexEx
    {
        /// <summary>
        /// 降序
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static IEnumerable<string> OrderByDescending(this IEnumerable<string> data)
        {
            return data.OrderByDescending(s => s.Length).Select(Regex.Escape);
        }

        /// <summary>
        /// 去除数字 OnlyDeleteEnd=是否只删除字符串末尾的数字
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="OnlyDeleteEnd">是否只删除字符串末尾的数字</param>
        /// <returns></returns>
        public static string ReplaceNum(this string data, bool OnlyDeleteEnd = false)
        {
            if (data == null) return null;
            if (OnlyDeleteEnd)
                return Regex.Replace(data, @"[0-9]+$", "");
            else
                return Regex.Replace(data, @"[0-9]", "");
        }

        /// <summary>
        /// 去除字母
        /// </summary>
        /// <param name="pData"></param>
        /// <returns></returns>
        public static string ReplaceLetter(this string data)
        {
            if (data == null) return null;
            return Regex.Replace(data, @"[a-zA-Z]", "");
        }


        /// <summary>
        /// 分割按照时间格式 2025/10/11 12:09:20
        /// </summary>
        /// <param name="pData"></param>
        /// <returns></returns>
        public static string[] SplitByDateTime(this string data)
        {
            if (string.IsNullOrEmpty(data))
                return new string[0];

            // 正则表达式模式 - 更精确地匹配日期时间块
            string pattern = @"\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}.*?(?=\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}|$)";

            // 使用Matches查找所有匹配项
            var matches = Regex.Matches(data, pattern, RegexOptions.Singleline);

            // 返回非空的匹配结果
            return matches.Cast<Match>()
                          .Select(m => m.Value.Trim())
                          .Where(s => !string.IsNullOrWhiteSpace(s))
                          .ToArray();
        }

        /// <summary>
        /// 去除控制符 \u0001等
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string RemoveControl(this string data)
        {
            return Regex.Replace(data, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F\x80-\x9F]", string.Empty);
        }

        /// <summary>
        /// 清理字符串：只保留 0-9 a-f A-F，删除回车、换行、空格、所有符号
        /// </summary>
        public static string CleanString(this string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
                return "";

            // 只保留你要的这一行
            return Regex.Replace(hexString, @"[^0-9a-fA-F]", "");
        }

        /// <summary>
        /// 查找 text 中所有 searchText 匹配位置（支持全词匹配和大小写）
        /// </summary>
        /// <param name="text">被查找的文本</param>
        /// <param name="searchText">要查找的关键字</param>
        /// <param name="wholeWord">是否全词匹配</param>
        /// <param name="caseSensitive">是否区分大小写</param>
        /// <returns>匹配位置(起始索引, 长度)集合</returns>
        public static IReadOnlyList<(int Start, int Length)> FindMatches(
            this string? text, string? searchText, bool wholeWord, bool caseSensitive = false)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchText))
                return Array.Empty<(int, int)>();

            if (wholeWord)
            {
                string pattern = $@"(?<!\w){Regex.Escape(searchText)}(?!\w)";
                RegexOptions options = RegexOptions.CultureInvariant;
                if (!caseSensitive) options |= RegexOptions.IgnoreCase;

                return Regex.Matches(text, pattern, options)
                    .Select(match => (match.Index, match.Length))
                    .ToArray();
            }

            var matches = new List<(int Start, int Length)>();
            int start = 0;
            while (start <= text.Length - searchText.Length)
            {
                StringComparison comparison = caseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;
                int index = text.IndexOf(searchText, start, comparison);
                if (index < 0) break;

                matches.Add((index, searchText.Length));
                start = index + searchText.Length;
            }

            return matches;
        }

    }
}
