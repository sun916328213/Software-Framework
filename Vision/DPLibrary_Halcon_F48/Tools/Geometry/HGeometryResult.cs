using Vision.Core.Tools;
 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools.Geometry
{
    public enum E_GeometryType
    {
        LineLine,
        LineCircle,
        PointCircle,
        PointLine,
        PointPoint,
        PointXLD,
        PointRegion
    }
    public class HLLResult : IToolResult
    {
        public bool IsFailed { get; set; }

        public string Name { get; set; }
        //交点
        public Point 直线交点 { get; set; }
        //夹角
        public double 直线夹角 { get; set; }
        //最小距离
        public double 最小距离 { get; set; }
        //最大距离
        public double 最大距离 { get; set; }


        public void Dispose()
        {
        }

        public override string ToString()
        {
            return Name + ";  " + (IsFailed ? "Failed" : "Success");
        }
    }
    public class HLCResult : IToolResult
    {
        public bool IsFailed { get; set; }

        public string Name { get; set; }

        //圆心到直线距离
        public double 圆心到直线距离 { get; set; }
        //圆到直线最短距离
        public double 直线到圆最短距离 { get; set; }



        public void Dispose()
        {
        }

        public override string ToString()
        {
            return Name + ";  " + (IsFailed ? "Failed" : "Success");
        }
    }
    public class HPCResult : IToolResult
    {
        public bool IsFailed { get; set; }

        public string Name { get; set; }

        //圆心到点距离
        public double 圆心到点距离 { get; set; }
        //圆到点最短距离
        public double 点到圆最短距离 { get; set; }



        public void Dispose()
        {
        }

        public override string ToString()
        {
            return Name + ";  " + (IsFailed ? "Failed" : "Success");
        }
    }

    public class HPLResult : IToolResult
    {
        public bool IsFailed { get; set; }

        public string Name { get; set; }

        //垂直距离
        public double 垂直距离 { get; set; }
        //垂点
        public Point 垂点坐标 { get; set; }

        public void Dispose()
        {
        }

        public override string ToString()
        {
            return Name + ";  " + (IsFailed ? "Failed" : "Success");
        }
    }

    public class HPPResult : IToolResult
    {
        public bool IsFailed { get; set; }

        public string Name { get; set; }

        //垂直距离
        public double 点到点距离 { get; set; }

        public void Dispose()
        {
        }

        public override string ToString()
        {
            return Name + ";  " + (IsFailed ? "Failed" : "Success");
        }
    }
}
