using Vision.Core.Tools;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools.AI.Definition
{
    /// <summary>
    /// 预测信息参数
    /// </summary>
    public class InferParam
    {
        /// <summary>
        /// 长边
        /// </summary>
        public double Long_Side;
        /// <summary>
        /// 短边
        /// </summary>
        public double Short_Side;
        /// <summary>
        /// 最小面积
        /// </summary>
        public double MinArea;
        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence = 0.5;
    }

    /// <summary>
    /// 推理结果轮廓信息集合
    /// </summary>
    public class InferResultInfo: IToolResult
    {
        /// <summary>
        /// Halcon轮廓和外接矩形
        /// </summary>
        public ContourInfo ContourInfo;
        /// <summary>
        /// 缺陷信息
        /// </summary>
        public DefectInfo DefectInfo;

        public string Name => DefectInfo.DefectName;
        public double  Area => DefectInfo.Area;
        public double Confidence => DefectInfo.Confidence;

        public bool IsFailed { get; set; }

        public override string ToString()
        {
            return $"结果:{DefectInfo.DefectName} ";
        }

        public void Dispose()
        {
             
        }
    }

    /// <summary>
    /// 推理结果信息  只用于程序结果 不对外输出
    /// </summary>
    internal class InferResult
    {
        /// <summary>
        /// 面积
        /// </summary>
        public double area;
        /// <summary>
        /// 置信度
        /// </summary>
        public double confidence;
        /// <summary>
        /// 推理结果标签
        /// </summary>
        public string class_name;
        /// <summary>
        /// 推理轮廓点X集合
        /// </summary>
        public HTuple Cols;
        /// <summary>
        /// 推理轮廓点Y集合
        /// </summary>
        public HTuple Rows;
        public InferResult() { }
        /// <summary>
        /// 推理结果信息
        /// </summary>
        /// <param name="area">缺陷面积</param>
        /// <param name="confidence">缺陷置信度</param>
        /// <param name="class_name">缺陷名称</param>
        /// <param name="cols">缺陷轮廓X集合</param>
        /// <param name="rows">缺陷轮廓Y集合</param>
        public InferResult(double area, double confidence, string class_name, HTuple cols, HTuple rows)
        {
            this.area = area;
            this.confidence = confidence;
            this.class_name = class_name;
            Cols = cols;
            Rows = rows;
        }
    }

    #region 
    /// <summary>
    /// 缺陷的外接矩形信息
    /// </summary>
    public class RectInfo
    {
        /// <summary>
        /// 外接矩形宽度
        /// </summary>
        public double Width;
        /// <summary>
        /// 外接矩形高度
        /// </summary>
        public double Height;
        /// <summary>
        /// 外接矩形长边
        /// </summary>
        public double LongSide;
        /// <summary>
        /// 外接矩形短边
        /// </summary>
        public double ShortSide;
        /// <summary>
        /// 外接矩形中心X
        /// </summary>
        public double CenterX;
        /// <summary>
        /// 外接矩形中心Y
        /// </summary>
        public double CenterY;
        /// <summary>
        /// 外接矩形原点X
        /// </summary>
        public double OriginX;
        /// <summary>
        /// 外接矩形原点Y
        /// </summary>
        public double OriginY;
        /// <summary>
        /// 外接矩形面积
        /// </summary>
        public double Area;
    }

    /// <summary>
    /// 缺陷的Halcon轮廓和外接矩形，用于Halcon界面显示
    /// </summary>
    public class ContourInfo
    {
        /// <summary>
        /// 轮廓 用于Halcon显示
        /// </summary>
        public HObject Xld;
        /// <summary>
        /// 最小外接矩形 用于Halcon显示
        /// </summary>
        public HObject Rectangle;
        /// <summary>
        /// 外接矩形信息
        /// </summary>
        public RectInfo RectInfo { get; set; }

        /// <summary>
        /// 缺陷的Halcon轮廓和外接矩形构造函数
        /// </summary>
        public ContourInfo() { }
        /// <summary>
        /// 缺陷的Halcon轮廓和外接矩形构造函数
        /// </summary>
        /// <param name="xld"></param>
        /// <param name="rect"></param>
        public ContourInfo(HObject xld, HObject rect)
        {
            Xld = xld; Rectangle = rect;
        }
    }

    /// <summary>
    /// 缺陷信息
    /// </summary>
    public class DefectInfo
    {
        /// <summary>
        /// 推理结果的名称
        /// </summary>
        public string DefectName { get; set; }
        /// <summary>
        /// 检测面积
        /// </summary>
        public double Area { get; set; }
        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }
    }
    #endregion
}
