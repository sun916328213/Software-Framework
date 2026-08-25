using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Tools.TemplateMatch;
using HalconDotNet;
using System.Diagnostics;
using System.IO;

using System.Text.Json.Serialization;

namespace DPLibrary_Halcon_F48.Tools
{
    /// <summary>
    /// 用于形状模板创建的工具
    /// </summary>
    public class HTool_TemplateControl : HBase_TemplateControl
    {
        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"Template_{ToolMge.Instance.GetToolCount<HTool_TemplateControl>() + 1}";
        /// <summary>
        /// 
        /// </summary>
        public Guid TemplateMatchToolID { get; set; }

        [JsonIgnore]
        /// <summary>
        /// 用于模版对应的匹配工具
        /// </summary>
        private HTool_TemplateMatch TpModel
        {
            get
            {
                return (HTool_TemplateMatch)ToolMge.Instance[TemplateMatchToolID];
            }
        }
        E_ToolType mToolType = E_ToolType.HTool_TemplateControl_SHP;
        public override E_ToolType ToolType { get { return mToolType; } }

        TemplateType mTemplateModelType = TemplateType.ShapeBasedMatching;
        public override TemplateType TemplateModelType 
        {
            get { return mTemplateModelType; }
            set 
            {
                if (mTemplateModelType != value)
                {
                    switch (value)
                    {
                        case TemplateType.ShapeBasedMatching:
                            mToolType = E_ToolType.HTool_TemplateControl_SHP;
                            break;
                        case TemplateType.GrayBasedMatching:
                            mToolType = E_ToolType.HTool_TemplateControl_NCC;
                            break;
                        default:
                            break;
                    }
                    mTemplateModelType = value;
                }
            }
        }

        public override void Config()
        {
            throw new NotImplementedException();
        }

        public override void Pause()
        {
            throw new NotImplementedException();
        }

        public override void Reset()
        {
            throw new NotImplementedException();
        }

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

            HObject hRegions = null;
            HObject temp = null;
            HObject roiImage = null;
            HObject gImage = null;
            HObject region = null;
            HTuple channels = null;
            HTuple inLevels = null, angleStart = null, angleExtent = null,
                    angleStep = null, scaleStep = null, contrast = null, minContrast = null;
            HTuple hv_NumLevels = null, hv_AngleStart = null, hv_AngleExtent = null, 
                   hv_AngleStep = null, hv_ScaleMin = null,hv_ScaleMax = null, 
                   hv_ScaleStep = null, hv_Metric = null, hv_MinContrast = null;
            HTuple area = null, row = null, col = null;
            HObject xld = null, xldTrans = null;
            HTuple homMat2D = null;

            try
            {
                if (SearchRegions.Count > 0)//有ROI
                {
                    temp?.Dispose();
                    roiImage?.Dispose();
                    hRegions = SearchRegions[0].GetRegion().Clone();
                    for (global::System.Int32 i = 1; i < SearchRegions.Count; i++)
                    {
                        HOperatorSet.Union2(hRegions, SearchRegions[i].GetRegion(), out temp);
                        hRegions.Dispose(); // 释放上一次的合并结果，避免内存泄漏
                        hRegions = temp;
                    }

                    HOperatorSet.ReduceDomain(mImage, hRegions, out roiImage);
                }
                else//全检
                {
                    roiImage = mImage.Clone();
                }

                inLevels = mLevels == -1 ? new HTuple("auto") : new HTuple(mLevels);
                angleStep = mAngleStep == -1 ? new HTuple("auto") : new HTuple(mAngleStep);
                angleStart = new HTuple(mAngleStart * Math.PI / 180.0);
                angleExtent = new HTuple(mAngleExtent * Math.PI / 180.0);
                scaleStep = mScaleStep == 0 ? new HTuple("auto") : new HTuple(mScaleStep);
                contrast = mContrast == -1 ? new HTuple("auto") : new HTuple(mContrast);
                minContrast = mMinContrast == -1 ? new HTuple("auto") : new HTuple(mMinContrast);



                switch (mTemplateModelType)
                {
                    case TemplateType.ShapeBasedMatching:
                        HOperatorSet.CreateScaledShapeModel(
                            roiImage,             // 模板图像
                            inLevels,               // 金字塔层数
                            angleStart,           // 起始角度 (弧度)
                            angleExtent,          // 角度范围 (弧度)
                            angleStep,               // 角度步长
                            mScaleMin,             // 最小缩放因子 (如 0.8 = 缩小 80%)
                            mScaleMax,             // 最大缩放因子 (如 1.2 = 放大 120%)
                            scaleStep,               // 缩放步长 (建议 "auto")
                            mOptimization.ToString(),
                            mHMetric.ToString(),       // 极性设置         
                            contrast,                
                            minContrast,               
                            out Model               // 输出模板句柄
                        );
                        HOperatorSet.GetShapeModelParams(Model,
                            out hv_NumLevels,
                            out hv_AngleStart,
                            out hv_AngleExtent,
                            out hv_AngleStep,
                            out hv_ScaleMin,
                            out hv_ScaleMax,
                            out hv_ScaleStep,
                            out hv_Metric,
                            out hv_MinContrast);
                        // 计算 ROI 的实际重心
                        HOperatorSet.AreaCenter(roiImage, out area, out row, out col);
                        HOperatorSet.GetShapeModelContours(out xld, Model, 1);
                        break;
                    case TemplateType.GrayBasedMatching:
                        HOperatorSet.CountChannels(roiImage, out channels);
                        if (channels.I > 1)
                        {
                            HOperatorSet.Rgb1ToGray(roiImage, out gImage);
                        }
                        else
                        {
                            gImage = roiImage;
                        }
                        HOperatorSet.CreateNccModel(
                            gImage,             // 模板图像
                            inLevels,               // 金字塔层数
                            angleStart,           // 起始角度 (弧度)
                            angleExtent,          // 角度范围 (弧度)
                            angleStep,               // 角度步长
                            "use_polarity",       // 极性设置      
                            out Model               // 输出模板句柄
                        );
                        HOperatorSet.GetNccModelParams(
                            Model,
                            out hv_NumLevels,
                            out hv_AngleStart,
                            out hv_AngleExtent,
                            out hv_AngleStep,
                            out hv_Metric);                
                        // 计算 ROI 的实际重心
                        HOperatorSet.AreaCenter(roiImage, out area, out row, out col);
                        HOperatorSet.GetNccModelRegion(out region, Model);
                        HOperatorSet.GenContourRegionXld(region, out xld, "border");    
                        break;
                    default:
                        break;
                }
                //更新标准值
                BasePoint = new System.Windows.Point(Math.Round(col.D, 3), Math.Round(row.D, 3));
                // 平移回 ROI 重心
                HOperatorSet.HomMat2dIdentity(out homMat2D);
                HOperatorSet.HomMat2dTranslate(homMat2D, row, col, out homMat2D);
                HOperatorSet.AffineTransContourXld(xld, out xldTrans, homMat2D);

                HXLD_Null xldResult = new HXLD_Null(xldTrans);

                ResultXLDs.Add(xldResult);
                Results.Add(new HTemplateResult()
                {
                    ResultXLD = xldResult,
                    Center = BasePoint
                });

                if (!DispModels.Contains(DisplayModel.Result))
                {
                    DispModels.Add(DisplayModel.Result);
                }
                DispLayer = (DisplayLayers)DisplayModel.Result;

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
                hRegions?.Dispose(); hRegions = null;
                temp?.Dispose(); temp = null;
                roiImage?.Dispose(); roiImage = null;

                gImage?.Dispose(); gImage = null;
                channels?.Dispose(); channels = null;

                inLevels?.Dispose(); inLevels = null;
                angleStep?.Dispose(); angleStep = null;
                angleStart?.Dispose(); angleStart = null;
                angleExtent?.Dispose(); angleExtent = null;
                scaleStep?.Dispose(); scaleStep = null;
                contrast?.Dispose(); contrast = null;
                minContrast?.Dispose(); minContrast = null;

                hv_NumLevels?.Dispose(); hv_NumLevels = null;
                hv_AngleStart?.Dispose(); hv_AngleStart = null;
                hv_AngleExtent?.Dispose(); hv_AngleExtent = null;
                hv_AngleStep?.Dispose(); hv_AngleStep = null;
                hv_ScaleMin?.Dispose(); hv_ScaleMin = null;
                hv_ScaleMax?.Dispose(); hv_ScaleMax = null;
                hv_ScaleStep?.Dispose(); hv_ScaleStep = null;
                hv_Metric?.Dispose(); hv_Metric = null;
                hv_MinContrast?.Dispose(); hv_MinContrast = null;
                area?.Dispose(); area = null;
                row?.Dispose(); row = null;
                col?.Dispose(); col = null;
                xld?.Dispose(); xld = null;
                xldTrans?.Dispose(); xldTrans = null;
                homMat2D?.Dispose(); homMat2D = null;
            }
        }

        public override void Save()
        {
            base.Save();
            var path = Path.Combine(FilePath, ToolType.ToString(), this.Name);
            if (Model != null)
            {
                if (Image != null)
                {
                    string fileImage = Path.Combine(path, $"{Name}_Image.bmp");
                    HOperatorSet.WriteImage(Image, "bmp", 0, fileImage);
                }
                string file;
                switch (mTemplateModelType)
                {
                    case TemplateType.ShapeBasedMatching:
                        file = Path.Combine(path, $"{Name}_template.shm");
                        HOperatorSet.WriteShapeModel(Model, file);
                        break;
                    case TemplateType.GrayBasedMatching:
                        file = Path.Combine(path, $"{Name}_template.ncm");
                        HOperatorSet.WriteNccModel(Model, file);
                        break;
                    default:
                        break;
                }
            }
        }

        // 反序列化完后，用参数重建运行期对象
        protected override void DoAfterDeserialized()
        {
            base.DoAfterDeserialized();
            // 反序列化完后，用参数重建运行期对象
            var path = Path.Combine(FilePath, ToolType.ToString(), this.Name);
            string[] files;
            switch (mTemplateModelType)
            {
                case TemplateType.ShapeBasedMatching:
                    files = Directory.GetFiles(path, "*.shm", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        string file = Path.Combine(FilePath, files[0]);
                        HOperatorSet.ReadShapeModel(file, out Model);
                    }
                    break;
                case TemplateType.GrayBasedMatching:
                    files = Directory.GetFiles(path, "*.ncm", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        string file = Path.Combine(FilePath, files[0]);
                        HOperatorSet.ReadNccModel(file, out Model);
                    }
                    break;
                default:
                    break;
            }

            string[] fileImage = Directory.GetFiles(path, $"{Name}_Image.bmp", SearchOption.TopDirectoryOnly);
            if (fileImage.Length > 0)
            {
                string file = Path.Combine(FilePath, fileImage[0]);
                HObject image;
                HOperatorSet.ReadImage(out image, file);
                Image = image;
            }
        }

        public override void OnGetTemplateImage(object arg1, EventArgs args)
        {
            var tp = TpModel;
            if (tp?.Image != null)
            {
                Image = tp.Image.Clone();
            }
        }
    }
}
