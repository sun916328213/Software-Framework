// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：SInferOutputName、AiditronHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、System、System.Collections.Generic、System.Drawing、System.Drawing.Imaging、System.Runtime.InteropServices。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P6 —— AiditronHelper 迁移（来源：DPLibrary_Halcon_F48/Tools/AI/Aiditron/AiditronHelp.cs）。
// 变更仅：命名空间 Vision.AI.Aiditron、类名 AiditronHelp→AiditronHelper、
// 基类/定义引用指向 Legacy 桥接。算法、SDK 调用、线程模型一字未动
// （含 tePredictThread 的吞异常旧行为——P6 不重写厂商流程，只做隔离）。
// 谛创 SDK = TeDemo/teProjsAdmin（teTreePredict.dll / teRuntime.dll，见 Sdk/）。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vision.AI.Aiditron.TeDemo;
using Vision.AI.Aiditron.Legacy;

namespace Vision.AI.Aiditron
{
    /// <summary>
    /// SInferOutputName 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public struct SInferOutputName
    {
        /// <summary>
        /// 工程名称
        /// </summary>
        public string ProjName { get; set; }
        /// <summary>
        /// 工程下需要输出的节点名称
        /// </summary>
        public List<string> OutputNodeName { get; set; }
    }

    /// <summary>
    /// AiditronHelper 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：AIBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal class AiditronHelper : AIBase
    {
        public int ImageMaxWidth = 4096;
        public int ImageMaxHeight = 4096;

        int Decimal = 3;
        teProjsAdmin m_clsProjsAdmin = new teProjsAdmin();
        int m_iMaxObjNum = 4096;
        List<int> ProjID = new List<int>();
        /// <summary>
        /// 预测结果
        /// </summary>
        List<TeObjectInSrc[][]> InferResult = new List<TeObjectInSrc[][]>();

        public override InferParam InferParam { get; set; } = new InferParam();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Inference。
        /// 正文中的关键协作调用包括 tePredictThread、AnalysisInfer_Seg_Cls_Reslut；具体条件和先后次序见方法体。
        /// </summary>
        public override List<InferResultInfo> Inference(HObject image)
        {
            if (InferParam == null) throw new Exception("InferenceParam Not set");
            tePredictThread(image);
            return AnalysisInfer_Seg_Cls_Reslut(image, InferResult, InferParam);
        }

        /// <summary>
        /// 数据分析 普通模式
        /// </summary>
        public List<InferResultInfo> AnalysisInfer_Reslut(HObject image, List<TeObjectInSrc[][]> Infer_Reslut, InferParam param)
        {
            List<InferResultInfo> cContours = new List<InferResultInfo>();
            for (int index = 0; index < Infer_Reslut.Count; index++)
            {
                for (int i = 0; i < Infer_Reslut[index].Length; i++)
                {
                    for (int j = 0; j < Infer_Reslut[index][i].Length; j++)
                    {
                        TeObjectInSrc teObjectInSrc = Infer_Reslut[index][i][j];

                        InferResultInfo iContour = GetInfer_Reslut(teObjectInSrc, image, param);
                        if (iContour != null)
                            cContours.Add(iContour);
                    }
                }
            }

            return cContours;
        }

        /// <summary>
        /// 数据分析 分割+分类
        /// </summary>
        public List<InferResultInfo> AnalysisInfer_Seg_Cls_Reslut(HObject image, List<TeObjectInSrc[][]> Infer_Reslut, InferParam param, bool ClsEnable = true)
        {
            List<InferResultInfo> cContours = new List<InferResultInfo>();
            for (int index = 0; index < Infer_Reslut.Count; index++)
            {
                for (int i = 0; i < Infer_Reslut[index].Length; i = i + 2)
                {
                    for (int j = 0; j < Infer_Reslut[index][i].Length; j++)
                    {
                        string Seg_name = System.Text.Encoding.UTF8.GetString(Infer_Reslut[index][i][j].pstrTag).Split('\0')[0];
                        string Cls_name = System.Text.Encoding.UTF8.GetString(Infer_Reslut[index][i + 1][j].pstrTag).Split('\0')[0];
                        if (Cls_name.ToLower().Contains("ng") || ClsEnable == false)
                        {
                            TeObjectInSrc teObjectInSrc = Infer_Reslut[index][i][j];
                            InferResultInfo iContour = GetInfer_Reslut(teObjectInSrc, image, param);
                            if (iContour != null)
                                cContours.Add(iContour);
                        }
                    }
                }
            }

            return cContours;
        }

        /// <summary>
        /// 获取推理结果
        /// </summary>
        public InferResultInfo GetInfer_Reslut(TeObjectInSrc teObjectInSrc, HObject image, InferParam param)
        {
            string Seg_name = System.Text.Encoding.UTF8.GetString(teObjectInSrc.pstrTag).Split('\0')[0];
            float confidence = teObjectInSrc.fProb;
            float Area = teObjectInSrc.fArea;
            int centercol = teObjectInSrc.stPosi.x + teObjectInSrc.stPosi.width / 2;
            int centerrow = teObjectInSrc.stPosi.y + teObjectInSrc.stPosi.height / 2;
            int m_w = teObjectInSrc.stPosi.width;
            int m_h = teObjectInSrc.stPosi.height;
            HTuple rows = new HTuple(), cols = new HTuple();
            for (var vv = 0; vv < teObjectInSrc.stPoints.Length; vv++)
            {
                cols.Append(teObjectInSrc.stPoints[vv].iX);
                rows.Append(teObjectInSrc.stPoints[vv].iY);
            }

            InferResult cInferResult = new InferResult(Area, confidence, Seg_name, cols, rows);
            var d1 = JudgeParam(cInferResult, param);
            if (d1 != null)
            {
                ContourInfo contourInfo = d1;
                HTuple w, h;
                HOperatorSet.GetImageSize(image, out w, out h);
                try
                {
                    InferResultInfo cContours = new InferResultInfo()
                    {
                        ContourInfo = contourInfo,
                        DefectInfo = new DefectInfo
                        {
                            DefectName = Seg_name,
                            Area = Math.Round(cInferResult.area, Decimal),
                        }
                    };
                    return cContours;
                }
                finally
                {
                    w.Dispose();
                    h.Dispose();
                }
            }

            return null;
        }

        /// <summary>
        /// 初始化模型
        /// </summary>
        public override void InitModel(string path, int type)
        {
            TeString strProjPath = new TeString();
            strProjPath.pString = System.IO.Path.GetFullPath(path);

            TeString[] pstrNodeName;
            int[] piParentIdx;
            int iNodeNum;
            int iSampleSize;
            int iCurProjId = m_clsProjsAdmin.teAddProj(ref strProjPath, out pstrNodeName, out piParentIdx, out iNodeNum, out iSampleSize);

            if (iCurProjId > -1)
            {
                ProjID.Add(iCurProjId);
                for (int i = 0; i < iNodeNum; i++)
                {
                    TeString stCurNodeName = new TeString();
                    stCurNodeName.pString = pstrNodeName[i].pString;
                    int iRst = m_clsProjsAdmin.teAddInitNode(iCurProjId, stCurNodeName);
                }
            }

            if (m_clsProjsAdmin.teInitProjs(ImageMaxWidth, ImageMaxHeight, 0, m_iMaxObjNum) != 0)
            {
                throw new Exception("init fail");
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 tePredictThread。
        /// 正文中的关键协作调用包括 Clear、teGetProjName、teGetInitNodeNum、AllocHGlobal、SizeOf；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private unsafe void tePredictThread(HObject image)
        {
            try
            {
                InferResult.Clear();
                for (int ii = 0; ii < ProjID.Count; ii++)
                {
                    int iProjId = (int)ProjID[ii];
                    string strProjName = m_clsProjsAdmin.teGetProjName(iProjId);
                    int iOutputNodeNum = m_clsProjsAdmin.teGetInitNodeNum(iProjId);

                    TeNodeRstDyn[] pstNodeRst = new TeNodeRstDyn[iOutputNodeNum];
                    for (int j = 0; j < iOutputNodeNum; j++)
                    {
                        pstNodeRst[j].iMaxObjNum = m_iMaxObjNum;
                        pstNodeRst[j].pstObjectInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TeObjectInSrc)) * m_iMaxObjNum);
                        pstNodeRst[j].pnLabelMat = Marshal.AllocHGlobal(sizeof(byte) * ImageMaxWidth * ImageMaxHeight);
                    }

                    TePredictParam[] pstParam = null;

                    TeImage[] InputImage = new TeImage[1];
                    Halcon2TeImage(out InputImage[0], image);
                    double time = m_clsProjsAdmin.tePredictDyn(iProjId, ref InputImage, ref pstNodeRst, ref pstParam);

                    TeObjectInSrc[][] pstObjectInSrc = new TeObjectInSrc[pstNodeRst.Length][];
                    for (int ij = 0; ij < pstNodeRst.Length; ij++)
                    {
                        m_clsProjsAdmin.teGetObjects(out pstObjectInSrc[ij], ref pstNodeRst[ij]);
                    }

                    InferResult.Add(pstObjectInSrc);
                }
            }
            catch (Exception e)
            {
                string str = e.Message + e.StackTrace;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teSetRandomColor2TeColor。
        /// 正文中的关键协作调用包括 Next；具体条件和先后次序见方法体。
        /// </summary>
        private void teSetRandomColor2TeColor(TeColor[] pstLabel2ColorTab, int iNum)
        {
            Random clsRandNum = new Random();
            for (int i = 0; i < iNum; i++)
            {
                pstLabel2ColorTab[i].Blue = (byte)clsRandNum.Next(0, 255);
                pstLabel2ColorTab[i].Red = (byte)clsRandNum.Next(0, 255);
                pstLabel2ColorTab[i].Green = (byte)clsRandNum.Next(0, 255);
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Halcon2TeImage。
        /// 正文中的关键协作调用包括 HImage2GrayTeImage；具体条件和先后次序见方法体。
        /// </summary>
        private void Halcon2TeImage(out TeImage TeImage, HObject image)
        {
            TeImage = HImage2GrayTeImage(image);
        }

        /// <summary>
        /// 将Halcon的图像转TeImage图像
        /// </summary>
        private TeImage HImage2GrayTeImage(HObject image)
        {
            TeImage stSrcImage = new TeImage();
            HOperatorSet.GetImagePointer1(image, out HTuple pointer, out HTuple type, out HTuple width, out HTuple height);
            try
            {
                Bitmap bmpImage = new Bitmap(width.I, height.I, PixelFormat.Format8bppIndexed);
                ColorPalette pal = bmpImage.Palette;
                for (int i = 0; i < 256; i++)
                {
                    pal.Entries[i] = Color.FromArgb(255, i, i, i);
                }

                bmpImage.Palette = pal;
                BitmapData bitmapData = bmpImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                int pixelSize = Bitmap.GetPixelFormatSize(bitmapData.PixelFormat) / 8;

                Int32 newDataWidth = ((System.Drawing.Image.GetPixelFormatSize(PixelFormat.Format8bppIndexed) * width) + 7) / 8;

                int stride = pixelSize * width;
                IntPtr dst = bitmapData.Scan0;
                IntPtr src = pointer;
                int length = Math.Abs(bitmapData.Stride) * bmpImage.Height;
                if (stride % 4 == 0)
                {
                    byte[] data = new byte[length];
                    System.Runtime.InteropServices.Marshal.Copy(src, data, 0, length);
                    System.Runtime.InteropServices.Marshal.Copy(data, 0, dst, length);
                }
                else
                {
                    stride = stride + 4 - stride % 4;
                    int length2 = width.I * pixelSize;
                    int count = length / stride;
                    for (int i = 0; i < count; i++)
                    {
                        byte[] data = new byte[length2];
                        System.Runtime.InteropServices.Marshal.Copy(src, data, 0, length2);
                        System.Runtime.InteropServices.Marshal.Copy(data, 0, dst, length2);
                        src += length2;
                        dst += stride;
                    }
                }

                bmpImage.UnlockBits(bitmapData);

                stSrcImage.iImgHeight = bmpImage.Height;
                stSrcImage.iImgWidth = bmpImage.Width;
                stSrcImage.iWidthStep = bitmapData.Stride;
                stSrcImage.pImgData = bitmapData.Scan0;
                stSrcImage.nChannels = 1;

                return stSrcImage;
            }
            finally
            {
                pointer.Dispose();
                type.Dispose();
                width.Dispose();
                height.Dispose();
            }
        }
    }
}
