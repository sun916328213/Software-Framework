using DPLibrary_Halcon_F48.Tools.AI.Definition;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPLibrary_Halcon_F48.Tools.AI
{
    public abstract class AIBase : IAI
    {
        /// <summary>
        /// 推理参数
        /// </summary>
        public abstract InferParam InferParam { get; set; }

        public abstract List<InferResultInfo> Inference(HObject image);
 
        public abstract void InitModel(string path, int type);

        //根据轮廓点获取轮廓和外接矩形
        internal virtual ContourInfo GetXldAndRect(HTuple rows, HTuple cols)
        {
            ContourInfo cContourInfo = new ContourInfo();

            HObject xld; HObject rect; RectInfo rectinfo;
         
            //将轮廓点组合成Region xld
            HOperatorSet.GenContourPolygonXld(out xld, rows, cols);

            HTuple row1, col1, phi, l1, l2;

            //获取Region的外接矩形信息
            HOperatorSet.SmallestRectangle2Xld(xld, out row1, out col1, out phi, out l1, out l2);
            //绘制外接矩形获取Region的xld
            HOperatorSet.GenRectangle2ContourXld(out rect, row1, col1, phi, l1, l2);

            HTuple row11, col11, row22, col22;
            //获取Region的外接矩形信息
            HOperatorSet.SmallestRectangle1Xld(xld, out row11, out col11, out row22, out col22);

            double width = l1;
            double heigh = l2;
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
        internal virtual ContourInfo JudgeParam(InferResult Infer_Reslut, InferParam param)
        {
            ContourInfo cContourInfo = null;
            // 置信度判断
            if (Infer_Reslut.confidence < param.Confidence) return null;

            cContourInfo = GetXldAndRect(Infer_Reslut.Rows, Infer_Reslut.Cols);

            //判断长短边和面积 小于设定标准
            if (cContourInfo.RectInfo.LongSide < param.Long_Side && cContourInfo.RectInfo.ShortSide < param.Short_Side && cContourInfo.RectInfo.Area < param.MinArea)
            {
                cContourInfo.Xld.Dispose();
                cContourInfo.Rectangle.Dispose();
                return null;
            }
            return cContourInfo;

        }
    }
}
