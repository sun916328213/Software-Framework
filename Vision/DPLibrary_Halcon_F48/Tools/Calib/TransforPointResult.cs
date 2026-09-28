using Vision.Core.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools
{
    public class TransforPointResult : IToolResult
    {
        /// <summary>
        /// 输入变换前的点位
        /// </summary>
        public Point InputPoint { get; set; }

        /// <summary>
        /// 未旋转的点位
        /// </summary>
        public Point OutputPoint_T { get; set; }

        /// <summary>
        /// 旋转后的点位
        /// </summary>
        public Point OutputPoint_TR { get; set; }
        public bool IsFailed { get; set; }

        /// <summary>
        /// X的偏移
        /// </summary>
        public double Dx { get; set; }
        /// <summary>
        /// Y的偏移
        /// </summary>
        public double Dy { get; set; }

        public override string ToString() 
        {
            return $"dx:{Dx} dy:{Dy}";
        }

        public void Dispose()
        {
        }
    }
}
