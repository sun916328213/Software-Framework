using Vision.Core.Tools;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPLibrary_Halcon_F48.Tools
{
    public class HMixtrueResult : IToolResult
    {

        public HTuple Mixtrue { get; set; }

        public bool IsFailed { get ; set ; }

        public void Dispose()
        {
            Mixtrue?.Dispose();
            Mixtrue = null;
        }
    }
}
