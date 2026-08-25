using DPLibrary_Base_F48.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools.TemplateMatch
{
    public class HTemplateResult : IToolResult
    {
        /// <summary>
        /// 分数
        /// </summary>
        public double Score { get; set; }
        /// <summary>
        /// 角度
        /// </summary>
        public double R { get; set; }
        /// <summary>
        /// 缩放
        /// </summary>
        public double Scale { get; set; }
        /// <summary>
        /// 中心坐标
        /// </summary>
        public Point Center { get; set; }

        [Browsable(false)]
        public I_HXLD ResultXLD { get; set; }
        public bool IsFailed { get; set; }

        public void Dispose()
        {
            ResultXLD.Dispose();
        }

        public override string ToString()
        {
            return $"分数:{Score} 点位:{Center}";
        }

    }
}
