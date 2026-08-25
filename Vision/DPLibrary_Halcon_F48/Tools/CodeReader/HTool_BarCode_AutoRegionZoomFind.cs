using DPLibrary_Base_F48;
using DPLibrary_Halcon_F48.Base.DispMessage;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Base_F48.Tools;
using System.Diagnostics;
using DPLibrary_Base_F48.Vision;

namespace DPLibrary_Halcon_F48.Tools
{
    /// <summary>
    /// 用于自动识别条码的工具
    /// </summary>
    public class HTool_BarCode_AutoRegionZoomFind: HTool_BarCode
    {
        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"BarCodeAuto_{ToolMge.Instance.GetToolCount<HTool_BarCode_AutoRegionZoomFind>() + 1}";


        [ToolPropertyAttribute(DisplayName = "二值化阈值", Group = "5.自动参数", Description = "用于分割背景区域",
            EditorType = E_PropertyEditorType.Int, Min = 1,Max = 255)]
        /// <summary>
        /// 二值化阈值
        /// </summary>
        public int ThresholdValue { get; set; } = 100;

        [ToolPropertyAttribute(DisplayName = "开操作卷积长", Group = "5.自动参数", Description = "卷积长度",
            EditorType = E_PropertyEditorType.Double, Min = 1)]
        /// <summary>
        /// 开操作卷积长
        /// </summary>
        public double OpenMask_Width { get; set; } = 40;

        [ToolPropertyAttribute(DisplayName = "开操作卷积高", Group = "5.自动参数", Description = "卷积高度",
            EditorType = E_PropertyEditorType.Double, Min = 1)]
        /// <summary>
        /// 开操作卷积高
        /// </summary>
        public double OpenMask_Heigh { get; set; } = 3;

        [ToolPropertyAttribute(DisplayName = "选择矩形的最小高度", Group = "5.自动参数", Description = "选择矩形的最小高度",
            EditorType = E_PropertyEditorType.Double, Min = 1)]
        public double Select_Height_Min { get; set; } = 70;

        [ToolPropertyAttribute(DisplayName = "选择矩形的最小宽度", Group = "5.自动参数", Description = "选择矩形的最小宽度",
            EditorType = E_PropertyEditorType.Double, Min = 1)]
        public double Select_Width_Min { get; set; } = 70;

        [ToolPropertyAttribute(DisplayName = "选择矩形的最小矩形度", Group = "5.自动参数", Description = "选择矩形的最小矩形度",
            EditorType = E_PropertyEditorType.Double, Min = 0.1)]
        public double Select_Rect_Min { get; set; } = 0.9;

        [ToolPropertyAttribute(DisplayName = "选择矩形的最大高度", Group = "5.自动参数", Description = "选择矩形的最大高度",
            EditorType = E_PropertyEditorType.Double, Min = 0.1)]
        public double Select_Height_Max { get; set; } = 130;

        [ToolPropertyAttribute(DisplayName = "选择矩形的最大宽度", Group = "5.自动参数", Description = "选择矩形的最大宽度",
            EditorType = E_PropertyEditorType.Double, Min = 0.1)]
        public double Select_Width_Max { get; set; } = 99999;

        [ToolPropertyAttribute(DisplayName = "选择矩形的最大矩形度", Group = "5.自动参数", Description = "选择矩形的最大矩形度",
            EditorType = E_PropertyEditorType.Double, Min = 0.1,Max = 1)]
        public double Select_Rect_Max { get; set; } = 1;

        [ToolPropertyAttribute(DisplayName = "膨胀核大小", Group = "5.自动参数", Description = "用于条码膨胀保留边缘",
            EditorType = E_PropertyEditorType.Double, Min = 0.1)]
        public double DilationMask { get; set; } = 3;

        Stopwatch sw = new Stopwatch();

        public override void Run()
        {
            //开始计时
            sw.Restart();
            RunMessage = "";
            ToolState = E_ToolState.Running;

            Results.Clear();
            ResultRegions.Clear();
            DispMessages.Clear();
            ResultXLDs.Clear();

            //自动寻找条码区域
            HObject region = null, region1 = null, opRegion = null, unionRegion = null, connectionRegion = null, selectRegions = null;

            try
            {
                HOperatorSet.Threshold(mImage, out region, 0, 255);//底图
                HOperatorSet.Threshold(mImage, out region1, ThresholdValue, 255);//背景图像

                HOperatorSet.OpeningRectangle1(region1, out opRegion, OpenMask_Width, OpenMask_Heigh);
                //求差
                HOperatorSet.Difference(region, opRegion, out unionRegion);
                //联通区域
                HOperatorSet.Connection(unionRegion, out connectionRegion);
                //挑选矩形区域
                HOperatorSet.SelectShape(connectionRegion, out selectRegions, new[] { "height", "width", "rectangularity" }, "and",
                                        new[] { Select_Height_Min, Select_Width_Min, Select_Rect_Min },
                                        new[] { Select_Height_Max, Select_Width_Max, Select_Rect_Max });
            }
            catch (Exception ex)
            {
                sw.Stop();
                RunTime = sw.ElapsedMilliseconds;
                ToolState = E_ToolState.Failed;
                RunMessage = ex.Message;
            }
            finally 
            {
                region?.Dispose(); region = null;
                region1?.Dispose(); region1 = null;
                opRegion?.Dispose(); opRegion = null;
                unionRegion?.Dispose(); unionRegion = null;
                connectionRegion?.Dispose(); connectionRegion = null;
            }

            //遍历每个选取，进行条码识别
            HTuple num = 0;
            HObject decodedRegions = null;
            HTuple decodedData = null;
            HTuple decodeTypes = new HTuple();
            HObject selectRegion = null;
            HObject ImageReduced = null, ImagePart = null;
            HObject imageZoomed = null;
            HTuple decodedTypes = null;
            HTuple decodedR = null;
            HTuple area = null, row = null, col = null;

            try
            {
                HOperatorSet.CountObj(selectRegions, out num);
                for (int i = 0; i < SelectCodeTypes.Count; i++)
                {
                    decodeTypes[i] = SelectCodeTypes[i].ToHalconString();
                }

                for (int i = 0; i < num.I; i++)
                {
                    selectRegion?.Dispose();
                    HOperatorSet.SelectObj(selectRegions, out selectRegion, i + 1);
                    HOperatorSet.DilationCircle(selectRegion, out selectRegion, DilationMask);//膨胀，增加边缘

                    ImageReduced?.Dispose();
                    ImagePart?.Dispose();
                    HOperatorSet.ReduceDomain(Image, selectRegion, out ImageReduced);
                    HOperatorSet.CropDomain(ImageReduced, out ImagePart);

                    imageZoomed?.Dispose();

                    List<HCodeResult> results = new List<HCodeResult>();

                    //放大5次
                    for (global::System.Int32 j = 0; j < 5; j++)
                    {
                        HOperatorSet.ZoomImageFactor(ImagePart, out imageZoomed, 1 + 0.3 * j, 1 + 0.3 * j, "constant");
                        HOperatorSet.FindBarCode(
                            imageZoomed,
                            out decodedRegions,
                            mBarcodeHandle,
                            decodeTypes,
                            out decodedData
                        );

                        if (decodedData.Length > 0)//有结果
                        {
                            decodedTypes?.Dispose();
                            decodedR?.Dispose();

                            HOperatorSet.GetBarCodeResult(mBarcodeHandle, "all", "decoded_types", out decodedTypes);
                            HOperatorSet.GetBarCodeResult(mBarcodeHandle, "all", "orientation", out decodedR);

                            results.Add(new HCodeResult()
                            {
                                Type = decodedTypes[0],
                                Code = decodedData[0],
                                R = Math.Round(decodedR[0].D, 3)
                            });
                        }
                    }

                    //缩小5次
                    for (global::System.Int32 j = 0; j < 5; j++)
                    {
                        HOperatorSet.ZoomImageFactor(ImagePart, out imageZoomed, 1 - 0.1 * j, 1 - 0.1 * j, "constant");
                        HOperatorSet.FindBarCode(
                            imageZoomed,
                            out decodedRegions,
                            mBarcodeHandle,
                            decodeTypes,
                            out decodedData
                        );

                        if (decodedData.Length > 0)//有结果
                        {
                            decodedTypes?.Dispose();
                            decodedR?.Dispose();

                            HOperatorSet.GetBarCodeResult(mBarcodeHandle, "all", "decoded_types", out decodedTypes);
                            HOperatorSet.GetBarCodeResult(mBarcodeHandle, "all", "orientation", out decodedR);

                            results.Add(new HCodeResult()
                            {
                                Type = decodedTypes[0],
                                Code = decodedData[0],
                                R = Math.Round(decodedR[0].D, 3)
                            });
                        }
                    }

                    //寻找最多项
                    var mostCommon = results
                        .Where(r => !string.IsNullOrEmpty(r.Code))   // 过滤掉空 Code
                        .GroupBy(r => r.Code)                        // 按 Code 分组
                        .OrderByDescending(x => x.Count())             // 按次数降序
                        .FirstOrDefault();                           // 取第一个


                    HCodeResult result = mostCommon?.FirstOrDefault();
                    Results.Add(result);
                    HROI_Null resultRegion = new HROI_Null(selectRegions.SelectObj(i + 1))
                    {
                        IsFull = false,
                        DispColor = E_VisionColor.Green
                    };
                    ResultRegions.Add(resultRegion);
                    result.ResultRegion = resultRegion;

                    if (result != null)
                    {
                        HOperatorSet.AreaCenter(selectRegion, out area, out row, out col);
                        result.Center = new System.Windows.Point(Math.Round(col.D, 3), Math.Round(row.D, 3));

                        DispMessages.Add(new HDispMessage()
                        {
                            Anchor = DispAnchor.Center,
                            UL = new System.Windows.Point(Math.Round(col.D, 3), Math.Round(row.D, 3)),
                            Message = $"{result.Code}"
                        });

                    }
                }

                if (Results.Count != 0)
                {
                    if (!DispModels.Contains(DisplayModel.Result))
                    {
                        DispModels.Add(DisplayModel.Result);
                    }
                    DispLayer = (DisplayLayers)DisplayModel.Result;
                }

                sw.Stop();
                RunTime = sw.ElapsedMilliseconds;
                ToolState = E_ToolState.RunCompleted;
            }
            catch (Exception ex)
            {
                sw.Stop();
                RunTime = sw.ElapsedMilliseconds;
                ToolState = E_ToolState.Failed;
                RunMessage = ex.Message;
            }
            finally 
            {
                area?.Dispose(); area = null;
                row?.Dispose(); row = null;
                col?.Dispose(); col = null;


                decodedTypes?.Dispose(); decodedTypes = null;
                decodedR?.Dispose(); decodedR = null;


                ImagePart?.Dispose(); ImagePart = null;
                ImageReduced?.Dispose(); ImageReduced = null;
                imageZoomed?.Dispose(); imageZoomed = null;

                selectRegion?.Dispose(); selectRegion = null;

                selectRegions?.Dispose(); selectRegions = null;

                num?.Dispose(); num = null;

                decodedRegions?.Dispose(); decodedRegions = null;
                decodedData?.Dispose(); decodedData = null;

                decodeTypes?.Dispose(); decodeTypes = null;
            }
        }
    }
}
