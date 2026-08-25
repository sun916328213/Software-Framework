using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPLibrary_Base_F48
{
    public static class DoubleEx
    {

        public static string ToArrString(this double[][] arr)
        {
            if (arr == null) return string.Empty;
            return string.Join(" | ", arr.Select(row => string.Join(", ", row)));
        }

        public static double[][] ToDoubles(this string str)
        {
            return str
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)   // 行
                .Select(r => r.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(v => double.Parse(v.Trim(), CultureInfo.InvariantCulture))
                                  .ToArray())
                .ToArray();
        }

    }
}
