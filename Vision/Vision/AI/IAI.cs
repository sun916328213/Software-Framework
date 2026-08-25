using HalconDotNet;
using System.Collections.Generic;
 
namespace Vision.AI
{
    /// <summary>
    /// 预测信息参数
    /// </summary>
    public class InferenceParam
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
        /// 矩形中心X
        /// </summary>
        public double CenterX { get; set; }
        /// <summary>
        /// 矩形中心Y
        /// </summary>
        public double CenterY { get; set; }
        /// <summary>
        /// 矩形信息 width
        /// </summary>
        public double Width { get; set; }
        /// <summary>
        /// 矩形信息 height
        /// </summary>
        public double Height { get; set; }
        /// <summary>
        /// 检测面积
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// 缺陷位置信息 半径-以notch6点钟为基准
        /// </summary>
        public double R { get; set; }
        /// <summary>
        /// 缺陷半径信息 角度-以notch6点钟为基准
        /// </summary>
        public double Angle { get; set; }
        /// <summary>
        /// 缺陷信息
        /// </summary>
        public DefectInfo() { }
        /// <summary>
        /// 缺陷信息
        /// </summary>
        /// <param name="icenterx">矩形中心X</param>
        /// <param name="icentery">矩形中心Y</param>
        /// <param name="iwidth">矩形宽度</param>
        /// <param name="ihenght">矩形高度</param>
        /// <param name="area">缺陷面积</param>
        /// <param name="r">缺陷位置信息 半径</param>
        /// <param name="angle">缺陷半径信息 角度</param>
        /// <param name="iname">推理结果的名称</param>
        public DefectInfo(string iname,double icenterx, double icentery, double iwidth, double ihenght, double area, double r, double angle  )
        {
            CenterX = icenterx; CenterY = icentery; Width = iwidth; Height = ihenght; Area = area;
            R = r; Angle = angle;
            DefectName = iname;
        }
        /// <summary>
        /// 缺陷信息
        /// </summary>
        /// <param name="objects"></param>
        public DefectInfo(object[] objects)
        {
            DefectName=objects[0].ToString();
            CenterX= (double)objects[1]; CenterY = (double)objects[2]; Width = (double)objects[3]; Height = (double)objects[4];
            Area = (double)objects[5]; R = (double)objects[6];
        }
    }
    #endregion

    /// <summary>
    /// 推理结果轮廓信息集合
    /// </summary>
    public class Contour
    {
        /// <summary>
        /// Halcon轮廓和外接矩形
        /// </summary>
        public ContourInfo ContourInfo;
        /// <summary>
        /// 缺陷信息
        /// </summary>
        public DefectInfo DefectInfo;
        /// <summary>
        /// 轮廓信息集合
        /// </summary>
        public Contour() { }
        /// <summary>
        /// 轮廓信息集合
        /// </summary>
        /// <param name="pContourInfo">Halcon轮廓和外接矩形</param>
        /// <param name="pDefectInfo"> 缺陷信息</param>
        public Contour( ContourInfo pContourInfo, DefectInfo pDefectInfo)
        {
            ContourInfo = pContourInfo;
            DefectInfo = pDefectInfo;
        }
    }

    /// <summary>
    /// 推理结果信息
    /// </summary>
    public class InferResult
    {
        /// <summary>
        /// 面积
        /// </summary>
        public float area;
        /// <summary>
        /// 置信度
        /// </summary>
        public float confidence;
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
        /// <summary>
        /// 推理结果信息
        /// </summary>
        public InferResult() { }
        /// <summary>
        /// 推理结果信息
        /// </summary>
        /// <param name="area">缺陷面积</param>
        /// <param name="confidence">缺陷置信度</param>
        /// <param name="class_name">缺陷名称</param>
        /// <param name="cols">缺陷轮廓X集合</param>
        /// <param name="rows">缺陷轮廓Y集合</param>
        public InferResult(float area, float confidence, string class_name, HTuple cols, HTuple rows)
        {
            this.area = area;
            this.confidence = confidence;
            this.class_name = class_name;
            Cols = cols;
            Rows = rows;
        }
    }

    public interface IAI
    {
        void InitModel(List<string> path);

        List<Contour > Inference (HObject image );
    }
}
