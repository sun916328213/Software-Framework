using DPLibrary_Halcon_F48.Tools.AI.Definition;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TeDemo;


namespace  DPLibrary_Halcon_F48.Tools.AI.AiDitron
{
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

    internal class AiditronHelp : AIBase
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

        public override InferParam InferParam { get; set; }

        public override List<InferResultInfo> Inference(HObject image)
        {
            if (InferParam == null) throw new Exception("InferenceParam Not set");
            tePredictThread(image);
            return AnalysisInfer_Seg_Cls_Reslut(image, InferResult, InferParam);
        }

        /// <summary>
        /// 数据分析 普通模式
        /// </summary>
        /// <param name="cContours"></param>
        /// <param name="image"></param>
        /// <param name="Infer_Reslut"></param>
        /// <param name="param"></param>
        public List<InferResultInfo> AnalysisInfer_Reslut(HObject image, List<TeObjectInSrc[][]> Infer_Reslut, InferParam param)
        {
            List<InferResultInfo> cContours = new List<InferResultInfo>();
            //先遍历工程
            for (int index = 0; index < Infer_Reslut.Count; index++)
            {
                //遍历缺陷类别
                for (int i = 0; i < Infer_Reslut[index].Length; i++)
                {
                    //遍历缺陷的具体位置和信息
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
        /// <param name="cContours">输出结果</param>
        /// <param name="image">图像</param>
        /// <param name="Infer_Reslut">推理结果</param>
        /// <param name="param">缺陷参数</param>
        /// <param name="ClsEnable">是否对分类的结果进行分析</param>
        public List<InferResultInfo> AnalysisInfer_Seg_Cls_Reslut(HObject image, List<TeObjectInSrc[][]> Infer_Reslut, InferParam param, bool ClsEnable = true)
        {
            List<InferResultInfo> cContours = new List<InferResultInfo>();
            //先遍历工程
            for (int index = 0; index < Infer_Reslut.Count; index++)
            {
                //遍历缺陷类别
                for (int i = 0; i < Infer_Reslut[index].Length; i = i + 2)
                {
                    //遍历缺陷的具体位置和信息
                    for (int j = 0; j < Infer_Reslut[index][i].Length; j++)
                    {

                        //获取分割缺陷类别
                        string Seg_name = System.Text.Encoding.UTF8.GetString(Infer_Reslut[index][i][j].pstrTag).Split('\0')[0];
                        //获取分类结果  当分类=NG时才判断分割的结果有效
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
        /// <param name="teObjectInSrc">推理结果</param>
        /// <param name="image"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public InferResultInfo GetInfer_Reslut(TeObjectInSrc teObjectInSrc, HObject image, InferParam param)
        {
            //获取分割缺陷类别
            string Seg_name = System.Text.Encoding.UTF8.GetString(teObjectInSrc.pstrTag).Split('\0')[0];
            //置信度
            float confidence = teObjectInSrc.fProb;
            float Area = teObjectInSrc.fArea;
            //获取缺陷坐标                     
            int centercol = teObjectInSrc.stPosi.x + teObjectInSrc.stPosi.width / 2;
            int centerrow = teObjectInSrc.stPosi.y + teObjectInSrc.stPosi.height / 2;
            int m_w = teObjectInSrc.stPosi.width;
            int m_h = teObjectInSrc.stPosi.height;
            //获取轮廓点位信息
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
            return null;
        }

        /// <summary>
        /// 初始化模型
        /// </summary>
        /// <param name="path">模型路径</param>
        /// <returns></returns>
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
                        //设置输出节点
                        TeString stCurNodeName = new TeString();
                        stCurNodeName.pString = pstrNodeName[i].pString;
                        int iRst = m_clsProjsAdmin.teAddInitNode(iCurProjId, stCurNodeName);
                    }
                }
        
            //初始化所有工程
            if (m_clsProjsAdmin.teInitProjs(ImageMaxWidth, ImageMaxHeight, 0, m_iMaxObjNum) != 0)
            {
                //初始化失败
                throw new Exception("init fail");
            }
        }



        //预测线程
        private unsafe void tePredictThread(HObject image)
        {
            try
            {
                InferResult.Clear();
                //循环预测所有工程
                for (int ii = 0; ii < ProjID.Count; ii++)
                {

                    int iProjId = (int)ProjID[ii]; //获取当前工程的ID
                    string strProjName = m_clsProjsAdmin.teGetProjName(iProjId);
                    int iOutputNodeNum = m_clsProjsAdmin.teGetInitNodeNum(iProjId);


                    TeNodeRstDyn[] pstNodeRst = new TeNodeRstDyn[iOutputNodeNum];
                    //分配Buffer以接收标签图
                    for (int j = 0; j < iOutputNodeNum; j++)
                    {
                        pstNodeRst[j].iMaxObjNum = m_iMaxObjNum;
                        pstNodeRst[j].pstObjectInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TeObjectInSrc)) * m_iMaxObjNum);
                        pstNodeRst[j].pnLabelMat = Marshal.AllocHGlobal(sizeof(byte) * ImageMaxWidth * ImageMaxHeight);
                    }

                    ////设定显示用的随机颜色
                    //TeColor[] pstLabel2ColorTab = new TeColor[(int)TeArrLen.MAX_CLASS_NUM];
                    //teSetRandomColor2TeColor(pstLabel2ColorTab, (int)TeArrLen.MAX_CLASS_NUM);

                    TePredictParam[] pstParam = null;

                    //图像格式转换
                    TeImage[] InputImage = new TeImage[1];
                    Halcon2TeImage(out InputImage[0], image);
                    //进行预测
                    double time = m_clsProjsAdmin.tePredictDyn(iProjId, ref InputImage, ref pstNodeRst, ref pstParam);

                    //结果转换
                    TeObjectInSrc[][] pstObjectInSrc = new TeObjectInSrc[pstNodeRst.Length][];
                    for (int ij = 0; ij < pstNodeRst.Length; ij++)
                    {
                        m_clsProjsAdmin.teGetObjects(out pstObjectInSrc[ij], ref pstNodeRst[ij]);//进行结果转换
                    }

                    InferResult.Add(pstObjectInSrc);
                }
            }
            catch (Exception e)
            {
                string str = e.Message + e.StackTrace;
            }
        }

        //随机生成一张标签-颜色表
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

        private void Halcon2TeImage(out TeImage TeImage, HObject image)
        {
            TeImage = HImage2GrayTeImage(image);
        }

        /// <summary>
        /// 将Halcon的图像转TeImage图像
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private TeImage HImage2GrayTeImage(HObject image)
        {
            TeImage stSrcImage = new TeImage();
            HOperatorSet.GetImagePointer1(image, out HTuple pointer, out HTuple type, out HTuple width, out HTuple height);
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

        
    }
}
