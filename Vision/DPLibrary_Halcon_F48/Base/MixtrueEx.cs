using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace DPLibrary_Halcon_F48.Base
{
    public static class MixtrueEx
    {
        public static string ToMixtrueStr(this HTuple hTuple) 
        {
            string result = string.Join("|", hTuple.DArr.Select((v, i) => new { v, i })
                                  .GroupBy(x => x.i / 3)
                                  .Select(g => string.Join(",", g.Select(x => x.v.ToString("F3")))));
            return result;
        }
        public static HTuple ToHMixtrue(this string str)
        {
            double[] ds = str.Split(new[] { '|',',' }, StringSplitOptions.RemoveEmptyEntries)  
                             .Select(v => double.Parse(v.Trim(), CultureInfo.InvariantCulture))
                             .ToArray();
            return new HTuple(ds);
        }
    }
}
