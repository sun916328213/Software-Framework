using DPLibrary_Base_F48.Tools;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools.Blob
{
    public class HBlobResult : IToolResult
    {
        public bool IsFailed { get; set; }

        public string Name { get; set; }

        public Point Center { get; set; }
        public string Area { get; set; }
        public HTuple features { get; set; }
        public HTuple values { get; set; }
        [Browsable(false)]
        public HROI ResultRegion { get; set; }

        public void Dispose()
        {
            ResultRegion.Dispose();

        }

        public override string ToString()
        {
            return Name + ";  " + (IsFailed ? "Failed" : "Success");
        }
    }

    
}
