using Vision.Core;
using Vision.Core.Tools;
using Vision.Core.Vision;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Base.DispMessage;
using Extension.UI.Controls;
using HalconDotNet;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Threading;

namespace DPLibrary_Halcon_F48.Tools
{
    public abstract class HVisionTool : IVisionTool
    {

        [JsonIgnore]
        protected HObject mImage;

        [JsonIgnore]
        protected DisplayLayers mDispLayer;

        [JsonPropertyName("ModelMixtrueID")]
        protected string mModelMixtrueID = Guid.Empty.ToString();

        [JsonIgnore]
        public DisplayLayers DispLayer
        {
            get { return mDispLayer; }
            set
            {
                mDispLayer = value;
                OnPropertyChanged(nameof(DispLayer));
            }
        }

        [JsonIgnore]
        [Browsable(false)]
        public BulkObservableCollection<DisplayModel> DispModels { get; set; } = new BulkObservableCollection<DisplayModel>();

        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "对齐矩阵", Group = "1.基础", Description = "用于模板匹配后对齐的矩阵", EditorType = E_PropertyEditorType.SingleSelect,
                               ListItemsSourceType = typeof(IVisionTool), ListItemsSourceName = nameof(AllMatrixs))]
        public MatrixData ModelMixtrue
        {
            get { return AllMatrixs.FirstOrDefault((id) => { return id.ToolID.ToString() == mModelMixtrueID; }); }
            set { mModelMixtrueID = (value?.ToolID ?? Guid.Empty).ToString(); }
        } 

        [ToolPropertyAttribute(DisplayName = "提示框颜色", Group = "1.基础", Description = "用于修改查找到的区域的颜色", EditorType = E_PropertyEditorType.SingleSelect)]
        /// <summary>
        ///提示框颜色
        /// </summary>
        public E_VisionColor DispRegionColor { get; set; } = E_VisionColor.Green;


        [ToolPropertyAttribute(DisplayName = "是否填充", Group = "1.基础", Description = "是否填充提示框", EditorType = E_PropertyEditorType.Checkbox)]
        /// <summary>
        ///是否填充
        /// </summary>
        public bool IsDispRegionFull { get; set; } = false;

        [JsonIgnore]
        public HObject Image
        {
            get { return mImage; }
            set
            {
                ClearDisp();
                mImage = value;
                DispModels.Clear();
                if (!DispModels.Contains(DisplayModel.Image))
                {
                    DispModels.Add(DisplayModel.Image);
                    if (!DispModels.Contains(DisplayModel.Image_ROI))
                    {
                        DispModels.Add(DisplayModel.Image_ROI);
                    }
                }
                // ⚠️ MeasurePath 必须带上：否则每次换图/执行后卡尺（MeasurePaths）都被隐藏。
                // 与 DisplayModel.Image_ROI 的枚举值、DisplayModelToLayersConverter.ConvertBack 保持一致。
                DispLayer = DisplayLayers.Image | DisplayLayers.ROI | DisplayLayers.MeasurePath;
                OnPropertyChanged(nameof(Image));
            }
        }

        [JsonIgnore]
        [Browsable(false)]
        /// <summary>
        /// 用于存放识别的结果区域
        /// </summary>
        public BulkObservableCollection<HROI> ResultRegions { get; set; } = new BulkObservableCollection<HROI>();
        [JsonIgnore]
        [Browsable(false)]
        /// <summary>
        /// 用于存放识别的结果轮廓
        /// </summary>
        public BulkObservableCollection<I_HXLD> ResultXLDs { get; set; } = new BulkObservableCollection<I_HXLD>();
        [JsonIgnore]
        [Browsable(false)]
        /// <summary>
        /// 用于存放提示信息
        /// </summary>
        public BulkObservableCollection<I_DispMessage> DispMessages { get; set; } = new BulkObservableCollection<I_DispMessage>();

        [Browsable(false)]
        /// <summary>
        /// Aoi区域，条码识别时会在此区域内进行搜索
        /// </summary>
        public BulkObservableCollection<HROI> SearchRegions { get; set; } = new BulkObservableCollection<HROI>();

        [Browsable(false)]
        /// <summary>
        /// 用于卡尺搜索的路径轮廓
        /// </summary>
        public BulkObservableCollection<HXLD2> MeasurePaths { get; set; } = new BulkObservableCollection<HXLD2>();



        public override void SaveSnapShot()
        {
            if (Image == null) return;

            HTuple width, height;
            HImage dumpImage = null;
            HWindow window = null;
            try
            {
                HOperatorSet.GetImageSize(Image, out width, out height);
                window = new HWindow(0, 0, width, height, new HTuple(0), "buffer", "");
                window.SetPart(0, 0, (height - 1).I, (width - 1).I);
                // 画图
                window.DispObj(Image);
                // 画Region
                if (ResultRegions != null)
                {
                    foreach (var region in ResultRegions)
                    {
                        region.Draw(window);
                    }
                }
                // 画xld
                if (ResultXLDs != null)
                {
                    foreach (var xld in ResultXLDs)
                    {
                        xld.Draw(window);
                    }
                }
                // 画文字
                if (DispMessages != null)
                {
                    foreach (var message in DispMessages)
                    {
                        message.Draw(window);
                    }
                }

                // 导出为 HImage（HObject）
                dumpImage = window.DumpWindowImage();

                HHelper.SaveImageAsync(dumpImage, AutoLogFilePath + $"\\{Name}\\{DateTime.Now.ToString("yyyy-MM-dd")}\\SnapshotImage", $"SnapshotImage_{DateTime.Now.ToString("HH-mm-ss-fff")}");
            }
            finally
            {
                // 确保截图或绘制异常时资源也能释放
                window?.CloseWindow();
                window?.Dispose();
                dumpImage?.Dispose();
            }
        }

        public void DrawXld(HObject xld)
        {
            I_HXLD item = new HXLD_Null(xld);
            ResultXLDs.Add(item);
        }

        public void DrawMessage(HDispMessage message)
        {
            DispMessages.Add(message);
        }

        protected virtual void ClearDisp()
        {
            Action action = () =>
            {
                Results?.Clear();
                ResultRegions?.Clear();
                DispMessages?.Clear();
            };
            action.UIInvoke();
        }
    }
}
