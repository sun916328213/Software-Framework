// ─────────────────────────────────────────────────────────────────────────────
// P6 —— AIBase 移植（来源：DPLibrary_Halcon_F48/Tools/AI/AIBase.cs）。
// 变更：去掉旧 IAI 接口继承（Inference(HObject) 绑定 HALCON 图像类型正是计划 §6
// 点名要解除的耦合；新入口走 Session + VisionImage）；保留 GetXldAndRect /
// JudgeParam 的轮廓外接矩形算法（AiditronHelper 沿用）。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;

namespace Vision.AI.Aiditron.Legacy
{
    /// <summary>AI helper 基类（轮廓外接矩形 + 缺陷判定；沿用迁移源算法）。</summary>
    internal abstract class AIBase
    {
        public abstract InferParam InferParam { get; set; }

        public abstract System.Collections.Generic.List<InferResultInfo> Inference(HObject image);

        public abstract void InitModel(string path, int type);

        internal virtual ContourInfo GetXldAndRect(HTuple rows, HTuple cols)
        {
            ContourInfo cContourInfo = new ContourInfo();

            HObject xld; HObject rect; RectInfo rectinfo;

            HOperatorSet.GenContourPolygonXld(out xld, rows, cols);

            HTuple row1, col1, phi, l1, l2;

            HOperatorSet.SmallestRectangle2Xld(xld, out row1, out col1, out phi, out l1, out l2);
            HOperatorSet.GenRectangle2ContourXld(out rect, row1, col1, phi, l1, l2);

            HTuple row11, col11, row22, col22;
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

        internal virtual ContourInfo JudgeParam(InferResult Infer_Reslut, InferParam param)
        {
            ContourInfo cContourInfo = null;
            if (Infer_Reslut.confidence < param.Confidence) return null;

            cContourInfo = GetXldAndRect(Infer_Reslut.Rows, Infer_Reslut.Cols);

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
