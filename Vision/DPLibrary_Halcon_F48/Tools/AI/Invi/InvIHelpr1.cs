using DPLibrary_Halcon_F48.Tools.AI;
using DPLibrary_Halcon_F48.Tools.AI.Definition;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
 

namespace DPLibrary_Halcon_F48.Tools.AI.Invi
{
    /// <summary>
    /// 业视软件流程  先分割->裁剪图像->分类
    /// </summary>
    internal class InvIHelpr1 : AIBase
    {
        InvIClassTool invIClassTool = new InvIClassTool();
        InvISegmentationTool invISegmentationTool = new InvISegmentationTool();
      
        int Decimal = 3;

        public override InferParam InferParam { get; set; }

        /// <summary>
        /// 加载模型  需要自己编写工具初始化
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public override void InitModel(string path,int  type)
        {
            bool Segresult = true, Claresult = true;
            //if (path.Count >= 1)
            //{
            //    //加载分割类型
            //    invISegmentationTool.base_param.SelectMode = "Offine";
            //    invISegmentationTool.base_param.ModelPath = path[0];
            //    Segresult = invISegmentationTool.InitInvIModel();
            //}
            //if (path.Count >= 2)
            //{
            //    //加载分类模型
            //    invIClassTool.class_base_param.SelectMode = "Offine";
            //    invIClassTool.class_base_param.ModelPath = path[1];
            //    Claresult = invIClassTool.InitInvIModel();
            //}
            if (Segresult == false || Claresult == false)
                throw new Exception("Init fail");
        }


        /// <summary>
        /// 推理数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        /// <param name="image"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public override List<InferResultInfo> Inference(HObject image)
        {

            invISegmentationTool.InputImage = image;
            //设置推理参数
            invISegmentationTool.run_param.long_side_range = Convert.ToInt32(InferParam.Long_Side);
            invISegmentationTool.run_param.short_side_range = Convert.ToInt32(InferParam.Short_Side);
            invISegmentationTool.run_param.filter_area = Convert.ToInt32(InferParam.MinArea);
            invISegmentationTool.run_param.confidence_threshold = InferParam.Confidence;
            //推理
            invISegmentationTool.Offline_Inference();

            return AnalysisInfer_Reslut(image, invISegmentationTool.Seg_Infer_Reslut, InferParam, "D:");
        }



        /// <summary>
        /// 分析数据结果
        /// </summary>
        /// <param name="cContours"></param>
        /// <param name="image"></param>
        /// <param name="Infer_Reslut"></param>
        /// <param name="param"></param>
        /// <param name="pPath"></param>
        public   List<InferResultInfo> AnalysisInfer_Reslut(HObject image, List<Vision.InvI.InvIToolMgr.InferResult> Infer_Reslut, InferParam param, string pPath)
        {
            List<InferResultInfo> cContours = new List<InferResultInfo>();
            //分析
            foreach (var item in Infer_Reslut)
            {
                // 获取轮廓点集合
                HTuple rows = new HTuple(), cols = new HTuple();
                for (var vv = 0; vv < item.contours.Count; vv++)
                {
                    cols.Append(item.contours.ElementAt(vv).x);
                    rows.Append(item.contours.ElementAt(vv).y);
                }

                InferResult cInferResult = new InferResult(item.area, item.confidence, item.class_name, cols, rows);

                var d1 = JudgeParam(cInferResult, param);
                if (d1 != null)
                {
                    var contourInfo = d1;
                    var rectInfo = contourInfo.RectInfo;

                    ////采集图像并存储
                    //HObject CropImage;
                    //HOperatorSet.CropPart(image, out CropImage, rectInfo.OriginY, rectInfo.OriginX, rectInfo.Width, rectInfo.Height);

                    //string path = Path.Combine(pPath, DateTime.Now.ToShortDateString());
                    //string name = DateTime.Now.ToString("HH-mm-ss-ms") + "_Crop";
                    //ImageHelper.Instance.SaveImage(CropImage, path, name);
                    //if (IsClass)
                    //{
                    //    //对裁剪后的图像进行分类预测
                    //    invIClassTool.InputImage = CropImage;
                    //    invIClassTool.Offline_Inference();
                    //    Bbox class_Reslut = invIClassTool.class_Reslut;
                    //    //判断分类结果名称和分割结果名称是否一致
                    //    if (item.class_name != class_Reslut.class_name) continue;
                    //}
                    HTuple w, h;
                    HOperatorSet.GetImageSize(image, out w, out h);

                    InferResultInfo Contour = new InferResultInfo()
                    {
                        ContourInfo = contourInfo,
                        DefectInfo = new DefectInfo
                        {
                            DefectName = item.class_name,
                            Area = Math.Round(cInferResult.area, Decimal),
                        }
                    };
                    cContours.Add(Contour);
                }
            }
            return cContours;
        }
    }
}
