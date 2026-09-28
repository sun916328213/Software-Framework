using Vision.Core.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools.Find
{
    public class HFindLineResult : IToolResult
    {
        public bool IsFailed { get ; set ; }

        public string Name { get; set; }

        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }

        public void Dispose()
        {
        }

        public override string ToString()
        {
            return Name + ";  " + (IsFailed ? "Failed":"Success");     
        }
    }

    internal class HFindCircleResult : IToolResult
    {
        public bool IsFailed { get; set; }

        public string Name { get; set; }

        public Point CenterPoint { get; set; }
        public double R { get; set; }

        public void Dispose()
        {
        }

        public override string ToString()
        {
            return Name + ";  " + (IsFailed ? "Failed" : "Success");
        }
    }
}
