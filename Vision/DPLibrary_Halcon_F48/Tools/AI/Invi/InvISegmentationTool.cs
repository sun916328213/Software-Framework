using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Vision.InvI;


namespace DPLibrary_Halcon_F48.Tools.AI.Invi
{
    /// <summary>
    /// 保存运行参数
    /// </summary>
    public class RunParam
    {
        /// <summary>
        /// 是否启用过滤参数
        /// </summary>
        public bool filter_parameter_flag { get; set; }

        /// <summary>
        /// 置信度阈值
        /// </summary>
        public double confidence_threshold { get; set; }

        /// <summary>
        /// 过滤面积设定值，此处只记录最小值
        /// </summary>
        public int filter_area { get; set; }

        /// <summary>
        /// 长边范围，此处只记录最小值
        /// </summary>
        public int long_side_range { get; set; }

        /// <summary>
        /// 短边范围，此处只记录最小值
        /// </summary>
        public int short_side_range { get; set; }

    }

    /// <summary>
    /// 业视分割算法工具类
    /// </summary>
    [Serializable]
    [Description("根据加载的分割模型，对于输入图像进行分割检测")]
    public class InvISegmentationTool 
    {
         #region 输入
        /// <summary>
        /// 输入图像
        /// </summary>
        [Description("输入图像")]
        public HObject  InputImage { get; set; }
         #endregion

        #region 输入图像的下标
        [Description("输入图像的下标")]
        public int InputImageIndex
        {
            get;
            set;
        }
        #endregion

        #region 输出
        /// <summary>
        /// 存储轮廓点的 object
        /// </summary>
        private List<HObject> Seg_Region_Objects = new List<HObject>();
        [Description("检测出的缺陷区域")]
        public List<HObject> DetectedDefectArea
        {
            get
            {
                return Seg_Region_Objects;
            }
        }

        /// <summary>
        /// 选择区域的面积
        /// </summary>
        [Description("所有缺陷区域的面积")]
        public HTuple Area
        {
            get
            {
                HTuple m_htArea = new HTuple();
                foreach (var obj in Seg_Infer_Reslut)
                {
                    m_htArea.Append(obj.area);
                }
                return m_htArea;
            }
        }

        /// <summary>
        /// 选择区域的X坐标
        /// </summary>
        [Description("所有缺陷区域的X坐标")]
        public HTuple X_Pos
        {
            get
            {
                HTuple m_htCol = new HTuple();
                foreach (var m_Region in Seg_Region_Objects)
                {
                    HTuple row1, col1, row2, col2;
                    HOperatorSet.SmallestRectangle1(m_Region, out row1, out col1, out row2, out col2);
                    m_htCol.Append(col1);
                }
                return m_htCol;
            }
        }
        /// <summary>
        /// 检测区域的Y坐标
        /// </summary>
        [Description("所有检测区域的Y坐标")]
        public HTuple Y_Pos
        {
            get
            {
                HTuple m_htRow = new HTuple();
                foreach (var m_Region in Seg_Region_Objects)
                {
                    HTuple row1, col1, row2, col2;
                    HOperatorSet.SmallestRectangle1(m_Region, out row1, out col1, out row2, out col2);
                    m_htRow.Append(row1);
                }
                return m_htRow;
            }
        }


        /// <summary>
        /// 每个缺陷区域的宽度
        /// </summary>
        [Description("每个缺陷区域的宽度")]
        public HTuple ObJ_W
        {
            get
            {
                HTuple m_htW = new HTuple();
                foreach (var m_Region in Seg_Region_Objects)
                {
                    HTuple row1, col1, row2, col2;
                    HOperatorSet.SmallestRectangle1(m_Region, out row1, out col1, out row2, out col2);
                    int m_w = col2 - col1;
                    m_htW.Append(m_w);
                }
                return m_htW;
            }
        }
        /// <summary>
        /// 每个缺陷区域的高度
        /// </summary>
        [Description("每个缺陷区域的高度")]
        public HTuple ObJ_H
        {
            get
            {
                HTuple m_htH = new HTuple();
                foreach (var m_Region in Seg_Region_Objects)
                {
                    HTuple row1, col1, row2, col2;
                    HOperatorSet.SmallestRectangle1(m_Region, out   row1, out   col1, out   row2, out   col2);
                    int m_h = row2 - row1;
                    m_htH.Append(m_h);
                }
                return m_htH;
            }
        }

        /// <summary>
        /// 默认缺陷的数量,如果是未运行,默认设置成 -1
        /// </summary>
        [Description("缺陷区域的数量")]
        public int Count
        {
            get
            {
                if (!Run_Infer_Flag)
                    return -1;
                return Seg_Infer_Reslut.Count();
            }
        }

        #endregion

        public BaseParam base_param = new BaseParam();
        public RunParam run_param = new RunParam();
        public List<InvIToolMgr.InferResult> Seg_Infer_Reslut = new List<InvIToolMgr.InferResult>();
        public bool Run_Infer_Flag = false;
        InvIToolMgr invIToolMgr = new InvIToolMgr();


        public InvISegmentationTool() : base()
        {
            // 基本参数
            base_param.SelectMode = null;
            base_param.ModelName = null;
            base_param.ServerPort = null;
            base_param.ServerIP = null;
            base_param.class_name = null;

            // 运行参数
            run_param.filter_parameter_flag = false;
            run_param.confidence_threshold = 0.5;
            run_param.filter_area = 1;
            run_param.long_side_range = 1;
            run_param.short_side_range = 1;
        }


        public bool InitInvIModel()
        {
            try
            {
                if (!invIToolMgr.CheckInvIModelStatus(base_param.ModelPath, ModelType.Segmentation))  // 说明该模型之前没有被加载过
                {
                    if (invIToolMgr.AddInvIClient(base_param.ModelPath, ModelType.Segmentation))
                    {
                        return true;
                    }
                    //     throw new Exception(string.Format(" InitInvIModel is error! base_param.ModelPath:{0}", base_param.ModelPath));
                    return false;
                }
            }
            catch
            {
                return false;
            }
            return true;
        }


        /// <summary>
        /// 本地推理  -1报错 0:OK  1:显示缺陷结果
        /// </summary>
        public int Offline_Inference()
        {
            try
            {
                //MessageBox.Show(string.Format("Offline_Inference InvISegTool start ModelPath:{0}", base_param.ModelPath));
                // 检查模型路径是否为空
                if (string.IsNullOrEmpty(base_param.ModelPath))
                {
                    throw new Exception(" Seg ModelPath is null! ");
                }
                if (InputImage == null)
                {
                    throw new Exception(" Seg Offline_Inference InputImage is null!");
                }

                if (!invIToolMgr.CheckInvIModelStatus(base_param.ModelPath, ModelType.Segmentation))
                {
                    if (!InitInvIModel())
                    {
                        throw new Exception(string.Format("InitInvIModel is error!  ModelPath:{0}", base_param.ModelPath));
                    }
                }

                while (invIToolMgr.GetInvIIsUsing(base_param.ModelPath, ModelType.Segmentation) == true)
                {
                    Thread.Sleep(50);
                }

                invIToolMgr.SetInvIIsUsing(base_param.ModelPath, ModelType.Segmentation, true);
                HImage hImage = new HImage(InputImage);
                var h_Channels = hImage.CountChannels();
                HTuple pointer_r = new HTuple();
                HTuple pointer_g = new HTuple();
                HTuple pointer_b = new HTuple();
                HTuple h_width = new HTuple();
                HTuple h_height = new HTuple();
                HTuple h_type = new HTuple();
                if (h_Channels == 1)
                {
                    HOperatorSet.GetImagePointer1(InputImage, out pointer_r, out h_type, out h_width, out h_height);
                    Seg_Infer_Reslut = invIToolMgr.Infer_Execute(base_param.ModelPath, ModelType.Segmentation, h_width, h_height,
                    h_Channels, pointer_r, IntPtr.Zero, IntPtr.Zero);

                }
                else if (h_Channels == 3)
                {
                    hImage.GetImagePointer3(out pointer_r, out pointer_g, out pointer_b, out h_type, out h_width, out h_height);
                    Seg_Infer_Reslut = invIToolMgr.Infer_Execute(base_param.ModelPath, ModelType.Segmentation, h_width, h_height,
                    h_Channels, pointer_r, pointer_g, pointer_b);
                }

                if (Seg_Infer_Reslut.Count() == 0)
                {
                    invIToolMgr.SetInvIIsUsing(base_param.ModelPath, ModelType.Segmentation, false);
                    throw new Exception("Seg_List_Reslut is null !");
                }
                //    throw new Exception("Success Seg_Infer_Reslut count:" + Seg_Infer_Reslut.Count());
                invIToolMgr.SetInvIIsUsing(base_param.ModelPath, ModelType.Segmentation, false);
                return 1;
            }
            catch
            {
                invIToolMgr.SetInvIIsUsing(base_param.ModelPath, ModelType.Segmentation, false);
                throw new Exception(string.Format("Offline_Inference get Seg_Infer_Reslut failed! base_param.ModelPath:{0}", base_param.ModelPath));
            }
        }

        /// <summary>
        /// 增加或更新选择特征
        /// </summary>
        /// <param name="strFeature"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void AddOrUpdateSelect(string strFeature, double min, double max)
        {

        }
    }
}
