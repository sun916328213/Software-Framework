// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：PositioningBaseInfo、PositioningInfo、BagPositioning。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.Halcon.Sessions、Vision.Tools.Halcon.Measurement、Vision.Tools.Halcon.Matching、Vision.Tools.Halcon.Identification、Vision.Tools.Halcon.Coordinate、Vision.Tools.Halcon.Calibration。导入不代表所有类型都会在运行时加载。
using Vision.Tools.Halcon.Sessions;
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.Halcon.Matching;
using Vision.Tools.Halcon.Identification;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Calibration;
using Vision.Core.Geometry;
using HalconDotNet;
using MachineApp.Business.Station;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WPFControls.ProgramControl.Services;

namespace MachineApp.Infra.VisionCore
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

        /// <summary>
        /// 初始化 BagPositioning 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public BagPositioning( )
        {
          
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Positioning。
        /// </summary>
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
        private static Point? Intersection(FindLineResult line1, FindLineResult line2)
        {
            double dx1 = line1.End.X - line1.Start.X;
            double dy1 = line1.End.Y - line1.Start.Y;
            double dx2 = line2.End.X - line2.Start.X;
            double dy2 = line2.End.Y - line2.Start.Y;
            double cross = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(cross) < 1e-8) return null;

            double dx3 = line1.Start.X - line2.Start.X;
            double dy3 = line1.Start.Y - line2.Start.Y;
            double factor = (dx3 * dy2 - dy3 * dx2) / cross;
            return new Point(
                line1.Start.X + factor * dx1,
                line1.Start.Y + factor * dy1);
        }
    }
}
