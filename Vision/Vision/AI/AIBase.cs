using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.AI
{
    internal abstract class AIBase : IAI
    {
        public abstract InferenceParam InferenceParam { get; set; }

        public abstract List<Contour> Inference(HObject image);

        public abstract void InitModel(List<string> path);

        //根据轮廓点获取轮廓和外接矩形
        public virtual ContourInfo GetXldAndRect(HTuple rows, HTuple cols)
        {
            ContourInfo cContourInfo = new ContourInfo();

            HObject xld; HObject rect; RectInfo rectinfo;
            HObject Region = new HObject();
            //将轮廓点组合成Region xld
            HOperatorSet.GenContourPolygonXld(out xld, rows, cols);

            HTuple row1, col1, phi, l1, l2;

            //获取Region的外接矩形信息
            HOperatorSet.SmallestRectangle2Xld(Region, out row1, out col1, out phi, out l1, out l2);
            //绘制外接矩形获取Region的xld
            HOperatorSet.GenRectangle2ContourXld(out rect, row1, col1, phi, l1, l2);

            HTuple row11, col11, row22, col22; 
            //获取Region的外接矩形信息
            HOperatorSet.SmallestRectangle1Xld(Region, out row11, out col11, out row22, out col22);

            int width = l1;
            int heigh = l2;
            rectinfo = new RectInfo();
            rectinfo.Width = width;
            rectinfo.Height = heigh;
            rectinfo.LongSide = width > heigh ? width : heigh;
            rectinfo.ShortSide = width < heigh ? width : heigh;
            rectinfo.CenterX = (col1);
            rectinfo.CenterY = (row1);
            rectinfo.Area = width * heigh;
            rectinfo.OriginX = col11;
            rectinfo.OriginY = col22;
            cContourInfo.Xld = xld;
            cContourInfo.Rectangle = rect;
            cContourInfo.RectInfo = rectinfo;
            return cContourInfo;
        }

        /// <summary>
        /// 判断缺陷是否大于设定标准
        /// </summary>
        /// <param name="Infer_Reslut">推理结果</param>
        /// <param name="param">设定参数</param>
        /// <returns>false=小于设定标准  true=大于设定标准</returns>
        public virtual ContourInfo JudgeParam(InferResult Infer_Reslut, InferenceParam param)
        {
            ContourInfo cContourInfo = null;
            // 置信度判断
            if (Infer_Reslut.confidence < param.Confidence) return null;

            cContourInfo = GetXldAndRect(Infer_Reslut.Rows, Infer_Reslut.Cols);

            //判断长短边和面积 小于设定标准
            if (cContourInfo.RectInfo.LongSide < param.Long_Side && cContourInfo.RectInfo.ShortSide < param.Short_Side && cContourInfo.RectInfo.Area < param.MinArea)
            {
                return null;
            }
            return cContourInfo;

        }

        /// <summary>
        /// 计算缺陷在硅片上的位置信息
        /// </summary>
        /// <param name="r"></param>
        /// <param name="angle"></param>
        /// <param name="center"></param>
        /// <param name="input"></param>
        public virtual void CalRadiusInfo(out double r, out double angle, PointF center, PointF input)
        {
            r = Math.Sqrt(Math.Pow(input.X - center.X, 2) + Math.Pow(input.Y - center.Y, 2));
            angle = Math.Atan2(input.X - center.X, input.Y - center.Y) / Math.PI * 180;
        }
    }
}
