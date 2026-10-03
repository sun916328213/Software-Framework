// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AIBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.AI.Aiditron、HalconDotNet。导入不代表所有类型都会在运行时加载。
using Vision.AI.Aiditron;
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Inference。
        /// </summary>
        public abstract System.Collections.Generic.List<InferResultInfo> Inference(HObject image);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 InitModel。
        /// </summary>
        public abstract void InitModel(string path, int type);

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetXldAndRect。
        /// 正文中的关键协作调用包括 GenContourPolygonXld、SmallestRectangle2Xld、GenRectangle2ContourXld、SmallestRectangle1Xld；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 JudgeParam。
        /// 正文中的关键协作调用包括 GetXldAndRect、Dispose；具体条件和先后次序见方法体。
        /// </summary>
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
