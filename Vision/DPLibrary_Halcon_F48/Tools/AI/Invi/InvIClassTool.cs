using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using Vision.InvI;

namespace DPLibrary_Halcon_F48.Tools.AI.Invi
{
    /// <summary>
    /// 保存基本参数
    /// </summary>
    public class BaseParam
    {
        /// <summary>
        /// 区域标签名称
        /// </summary>
        public string class_name { get; set; }

        /// <summary>
        /// 选择模式
        /// </summary>
        public string SelectMode { get; set; }

        /// <summary>
        /// 服务器IP
        /// </summary>
        public string ServerIP { get; set; }

        /// <summary>
        /// 服务器端口
        /// </summary>
        public string ServerPort { get; set; }

        /// <summary>
        /// 模型名称
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// 离线模式下模型路径
        /// </summary>
        public string ModelPath { get; set; }
    }

    /// <summary>
    /// 业视分类算法工具类
    /// </summary>
    [Serializable]
    [Description("根据加载的AI模型，对于输入图像进行检测")]
    public class InvIClassTool 
    {
        //#region 输入
        /// <summary>
        /// 输入图像
        /// </summary>
        [Description("输入图像")]
        public HObject  InputImage { get; set; }
        //#endregion

        #region 输出
        /// <summary>
        /// 检测出的缺陷下标
        /// </summary>
        [Description("检测出的缺陷下标")]
        public int ClassID
        {
            get
            {
                return class_Reslut.id;
            }
        }

        /// <summary>
        /// 检测出的缺陷名称
        /// </summary>
        [Description("检测出的缺陷类别标签名称")]
        public string ClassName
        {
            get
            {
                return class_Reslut.class_name;
            }
        }

        /// <summary>
        /// 检测出的缺陷置信度
        /// </summary>
        [Description("检测出的缺陷类别标签置信度")]
        public float ClassScore
        {
            get
            {
                return class_Reslut.confidence;
            }
        }

        #endregion

        public BaseParam class_base_param = new BaseParam();
        public InvIToolMgr.Bbox class_Reslut = new InvIToolMgr.Bbox();
        InvIToolMgr invIToolMgr=new InvIToolMgr() ;

        public InvIClassTool() : base()
        {
            class_base_param.SelectMode = "OnLine";
            class_base_param.ModelName = null;
            class_base_param.ServerPort = null;
            class_base_param.ServerIP = null;

            class_Reslut.id = -1;     // 初始化
        }

        public bool InitInvIModel()
        {
            try
            {
                if (!invIToolMgr.CheckInvIModelStatus(class_base_param.ModelPath, ModelType.Classification)) // 说明该模型之前没有被加载过
                {
                    return invIToolMgr.AddInvIClient(class_base_param.ModelPath, ModelType.Classification);
                }
            }
            catch (Exception)
            {
              //      throw new Exception.Show("加载离线模型失败:" + ex);

                return false;
            }
            return false;
        }


        /// <summary>
        /// 本地推理
        /// </summary>
        public bool Offline_Inference()
        {
            try
            {
                if (InputImage == null)
                {
                    //       throw new Exception.Show (" InputImage  is null!");
                    return false;
                }

                //      throw new Exception.Show("Offline_Inference InvIClassTool start");
                if (!invIToolMgr.CheckInvIModelStatus(class_base_param.ModelPath, ModelType.Classification))
                {
                    if (!InitInvIModel())
                    {
                        //        throw new Exception.Show("InitInvIModel is error!");
                        return false;
                    }
                }

                while (invIToolMgr.GetInvIIsUsing(class_base_param.ModelPath, ModelType.Classification) == true)
                {
                    Thread.Sleep(50);
                }

                invIToolMgr.SetInvIIsUsing(class_base_param.ModelPath, ModelType.Classification, true);
                HImage hImage = new HImage(InputImage);
                var h_Channels = hImage.CountChannels();
                HTuple pointer_r = new HTuple();
                HTuple pointer_g = new HTuple();
                HTuple pointer_b = new HTuple();
                HTuple h_width = new HTuple();
                HTuple h_height = new HTuple();
                HTuple h_type = new HTuple();

                List<InvIToolMgr.InferResult> list_Infer_result = new List<InvIToolMgr.InferResult>();
                if (h_Channels == 1)
                {
                    HOperatorSet.GetImagePointer1(InputImage, out pointer_r, out h_type, out h_width, out h_height);
                    list_Infer_result = invIToolMgr.Infer_Execute(class_base_param.ModelPath, ModelType.Classification, h_width, h_height,
                    h_Channels, pointer_r, IntPtr.Zero, IntPtr.Zero);
                }
                else if (h_Channels == 3)
                {
                    hImage.GetImagePointer3(out pointer_r, out pointer_g, out pointer_b, out h_type, out h_width, out h_height);
                    list_Infer_result = invIToolMgr.Infer_Execute(class_base_param.ModelPath, ModelType.Classification, h_width, h_height,
                    h_Channels, pointer_r, pointer_g, pointer_b);
                }

                if (list_Infer_result.Count() == 0)
                {
                    class_Reslut.id = -1;

                    invIToolMgr.SetInvIIsUsing(class_base_param.ModelPath, ModelType.Classification, false);
                    throw new Exception("class_Reslut is null !");
                }
                else
                {
                    class_Reslut.id = list_Infer_result.ElementAt(0).id;
                    class_Reslut.class_name = list_Infer_result.ElementAt(0).class_name;
                    class_Reslut.confidence = (float)Math.Round(list_Infer_result.ElementAt(0).confidence, 2);
                    //    throw new Exception.Show("cls_result:" + class_Reslut.class_name);
                    invIToolMgr.SetInvIIsUsing(class_base_param.ModelPath, ModelType.Classification, false);
                    return true;
                }
            }
            catch
            {
                invIToolMgr.SetInvIIsUsing(class_base_param.ModelPath, ModelType.Classification, false);
                throw new Exception(string.Format("Offline_Inference get class_Reslut failed! base_param.ModelPath:{0}", class_base_param.ModelPath));
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
