using DPLibrary_Halcon_F48.Tools.Find;
using DPLibrary_Halcon_F48.Tools.FindLine;
using HalconDotNet;
using Packing_Machine.Business.Station;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.Infra.VisionCore
{
    /// <summary>
    /// 袋定位基准信息
    /// </summary>
    public class PositioningBaseInfo
    {
        /// <summary>
        /// 基准宽度
        /// </summary>
        public double BaseWidth { get; set; }
        /// <summary>
        /// 左侧基准
        /// </summary>
        public Point Left { get; set; }
        /// <summary>
        /// 右侧基准
        /// </summary>
        public Point Right { get; set; }

    }

    /// <summary>
    /// 袋定位结果
    /// </summary>
    public class PositioningInfo
    {
        /// <summary>
        /// 宽度
        /// </summary>
        public double Width { get; set; }
    }

    /// <summary>
    /// 袋定位
    /// </summary>
    internal class BagPositioning
    {
        PositioningBaseInfo positioningBaseInfo=new PositioningBaseInfo();

       public double LeftPixelRadio, RightPixelRadio;

        public BagPositioning( )
        {
          
        }

        public void Positioning(Point point1, Point point2)
        {
            var w1 = (point1 - positioningBaseInfo.Left) * LeftPixelRadio + (point1 - positioningBaseInfo.Right) * RightPixelRadio;
            double w2 = w1.X + positioningBaseInfo.BaseWidth;
            PositioningInfo cALInfo = new PositioningInfo() { Width = w2 };
        }


        /// <summary>
        /// 示教袋定位信息
        /// </summary>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        /// <param name="baseWidth"></param>
        private void Teach( Point point1, Point point2, double baseWidth)
        {
            positioningBaseInfo.Left = point1;
            positioningBaseInfo.Right = point2;
            positioningBaseInfo.BaseWidth = baseWidth;
        }


      
        /// <summary>
        /// 获取两个线段相交点 在Vision中实现
        /// </summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <returns></returns>
        private static Point? Intersection(HFindLineResult line1, HFindLineResult line2)
        {
            double dx1 = line1.EndPoint.X - line1.StartPoint.X;
            double dy1 = line1.EndPoint.Y - line1.StartPoint.Y;
            double dx2 = line2.EndPoint.X - line2.StartPoint.X;
            double dy2 = line2.EndPoint.Y - line2.StartPoint.Y;
            double cross = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(cross) < 1e-8) return null;

            double dx3 = line1.StartPoint.X - line2.StartPoint.X;
            double dy3 = line1.StartPoint.Y - line2.StartPoint.Y;
            double factor = (dx3 * dy2 - dy3 * dx2) / cross;
            return new Point(
                line1.StartPoint.X + factor * dx1,
                line1.StartPoint.Y + factor * dy1);
        }
    }
}
