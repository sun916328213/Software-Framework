using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Tools.TemplateMatch;
using HalconDotNet;
using System.Diagnostics;

using System.Text.Json.Serialization;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools
{
    /// <summary>
    /// 用于形状模板匹配的工具
    /// </summary>
    public class HTool_TemplateMatch : HBase_TemplateMatch
    {
        public HTool_TemplateMatch() :base()
        {
            Outputs = new ToolOutputPorts(new List<IToolOutputPort>()
            {
                new OutputPort<HTemplateResult>(ID.ToString(), "HTemplateResult") 
            }); 
        }

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"TemplateMatch_{ToolMge.Instance.GetToolCount<HTool_TemplateMatch>() + 1}";


        public override E_ToolType ToolType { get { return E_ToolType.HTool_TemplateMatch; } }
        [JsonIgnore]
        /// <summary>
        /// 用于存储模板信息
        /// </summary>
        public HTool_TemplateControl mTpModel 
        {
            get 
            {
                return (HTool_TemplateControl)ToolMge.Instance[ModelID];
            }
        }

        protected override void OnChangeTemplateModel_Clicked(object sender, EventArgs e) 
        {
            if (mTpModel == null)
            {
                HTool_TemplateControl tool = new HTool_TemplateControl();
                ToolMge.Instance.Add(tool);
                ModelID = tool.ID;
            }
            mTpModel.TemplateMatchToolID = ID;
            RaiseRequestShowView(mTpModel);
        }

        public override void Config()
        {
      
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

            // 查找模板
            HTuple hv_Row = 0, hv_Column = 0, hv_Angle = 0, hv_Scale = 0, hv_Score = 0;
            HObject hRegions = null;
            HObject temp = null;
            HObject roiImage = null;
            HObject gImage = null;
            HObject region = null;
            HTuple channels = null;

            HTuple angleStart = null, angleExtent = null,
                    scaleMix = null,scaleMax = null,
                    subPixel = null;
            HObject ho_ModelContour = null, ho_TransContour = null;

            HTuple hv_HomMat2DScaled = null, hv_HomMat2DRotate = null, hv_HomMat2DTrans = null;

            HTuple hv_HomMat2D = null;

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
                    roiImage = Image?.Clone();
                }

                if (mTpModel == null)
                {
                    MessageBox.Show("请先设置模版！！");
                    throw new Exception("未设置模版,请先设置模版");
                }

                angleStart = new HTuple(mTpModel.AngleStart * Math.PI / 180.0);
                angleExtent = new HTuple(mTpModel.AngleExtent * Math.PI / 180.0);
                scaleMix = new HTuple(mTpModel.ScaleMin);
                scaleMax = new HTuple(mTpModel.ScaleMax);

                if (mDeformation != 0)//存在形变
                {
                    subPixel = new HTuple(new string[] { mSubPixel.ToString(), $"max_deformation {mDeformation}" });
                }
                else
                {
                    subPixel = mSubPixel.ToString();
                }

                ho_ModelContour?.Dispose(); ho_ModelContour = null;

                switch (mTpModel.TemplateModelType)
                {
                    case TemplateType.ShapeBasedMatching:
                        HOperatorSet.FindScaledShapeModel(
                            roiImage,           // 搜索图像
                            mTpModel.Model,              // 模型ID
                            angleStart,           // 搜索角度起始值
                            angleExtent,        // 搜索角度范围
                            scaleMix,
                            scaleMax,
                            mMinScore,                // 最低匹配分数 (0~1)
                            mNumMatches,                  // 最大匹配个数
                            mMaxOverlap,                // 最大重叠度
                            subPixel,    // 匹配方法
                            0,                  // 0=用建模时的层数
                            mGreediness,                // 搜索贪婪度
                            out hv_Row,
                            out hv_Column,
                            out hv_Angle,
                            out hv_Scale,
                            out hv_Score
                        );
                        // 获取模型轮廓
                        HOperatorSet.GetShapeModelContours(out ho_ModelContour, mTpModel.Model, 1);
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
                        HOperatorSet.FindNccModel(
                            gImage,           // 搜索图像
                            mTpModel.Model,              // 模型ID
                            angleStart,           // 搜索角度起始值
                            angleExtent,        // 搜索角度范围
                            mMinScore,                // 最低匹配分数 (0~1)
                            mNumMatches,                  // 最大匹配个数
                            mMaxOverlap,                // 最大重叠度
                            "true",
                            0,                  // 0=用建模时的层数
                            out hv_Row,
                            out hv_Column,
                            out hv_Angle,
                            out hv_Score
                        );
                        HOperatorSet.GetNccModelRegion(out region, mTpModel.Model);
                        HOperatorSet.GenContourRegionXld(region, out ho_ModelContour, "border");
                        break;
                    default:
                        break;
                }

                // 判断是否找到
                if (hv_Score.Length > 0)
                {
                    for (global::System.Int32 i = 1; i < hv_Score.Length + 1; i++)
                    {
                        ho_TransContour?.Dispose(); ho_TransContour = null;
                        // 将模型轮廓变换到匹配位置
                        hv_HomMat2D?.Dispose(); hv_HomMat2D = null;
                        //单位矩阵
                        HOperatorSet.HomMat2dIdentity(out hv_HomMat2D);
                        //// 缩放
                        switch (mTpModel.TemplateModelType)
                        {
                            case TemplateType.ShapeBasedMatching:
                                HOperatorSet.HomMat2dScale(hv_HomMat2D, hv_Scale[i - 1], hv_Scale[i - 1], 0, 0, out hv_HomMat2DScaled);
                                break;
                            case TemplateType.GrayBasedMatching:
                                hv_HomMat2DScaled = hv_HomMat2D;
                                break;
                            default:
                                break;
                        }
                        //// 旋转
                        HOperatorSet.HomMat2dRotate(hv_HomMat2DScaled, hv_Angle[i - 1], 0, 0, out hv_HomMat2DRotate);
                        // 平移到匹配位置 
                        HOperatorSet.HomMat2dTranslate(hv_HomMat2DRotate, hv_Row[i - 1], hv_Column[i - 1], out hv_HomMat2DTrans);
                        // 变换轮廓
                        HOperatorSet.AffineTransContourXld(ho_ModelContour, out ho_TransContour, hv_HomMat2DTrans);

                        HXLD_Null xldResult = new HXLD_Null(ho_TransContour.Clone());
                        ResultXLDs.Add(xldResult);

                        //Outputs[0]

                        Outputs["HTemplateResult"].Set(ToolMge.Instance.ToolBus, new HTemplateResult()
                        {
                            Score = Math.Round(hv_Score[i - 1].D, 3),
                            R = Math.Round(hv_Angle[i - 1].D, 3) / 3.14 * 180,
                            Scale = Math.Round(hv_Scale[i - 1].D, 3),
                            Center = new System.Windows.Point(Math.Round(hv_Column[i - 1].D, 3), Math.Round(hv_Row[i - 1].D, 3)),
                            ResultXLD = xldResult
                        });
                        Results.Add(new HTemplateResult()
                        {
                            Score = Math.Round(hv_Score[i - 1].D, 3),
                            R = Math.Round(hv_Angle[i - 1].D, 3)/ 3.14 * 180,
                            Scale = Math.Round(hv_Scale[i - 1].D, 3),
                            Center = new System.Windows.Point(Math.Round(hv_Column[i - 1].D, 3), Math.Round(hv_Row[i - 1].D, 3)),
                            ResultXLD = xldResult
                        });
                    }
                }

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
                hv_Row?.Dispose(); hv_Row = null;
                hv_Column?.Dispose(); hv_Column = null;
                hv_Angle?.Dispose(); hv_Angle = null; 
                hv_Scale?.Dispose(); hv_Scale = null; 
                hv_Score?.Dispose(); hv_Score = null;

                hRegions?.Dispose(); hRegions = null;
                temp?.Dispose(); temp = null;
                roiImage?.Dispose(); roiImage = null;
                gImage?.Dispose(); gImage = null;
                region?.Dispose(); region = null;
                channels?.Dispose();channels = null;
                angleStart?.Dispose(); angleStart = null;
                angleExtent?.Dispose(); angleExtent = null;
                scaleMix?.Dispose(); scaleMix = null;
                scaleMax?.Dispose(); scaleMax = null;
                subPixel?.Dispose(); subPixel = null;

                ho_ModelContour?.Dispose(); ho_ModelContour = null;
                ho_TransContour?.Dispose(); ho_TransContour = null;
                hv_HomMat2DScaled?.Dispose(); hv_HomMat2DScaled = null;
                hv_HomMat2DRotate?.Dispose(); hv_HomMat2DRotate = null;
                hv_HomMat2DTrans?.Dispose(); hv_HomMat2DTrans = null;
                hv_HomMat2D?.Dispose(); hv_HomMat2D = null;
            }
        }

        public override void Save()
        {
            base.Save();
            if (mTpModel != null)
            {
                mTpModel.Save();
            }
        }


        protected override void DoAfterDeserialized()
        {
            base.DoAfterDeserialized();
            Outputs = null;
            Outputs = new ToolOutputPorts(new List<IToolOutputPort>()
            {
                new OutputPort<HTemplateResult>(ID.ToString(), "HTemplateResult")
            });
        }

    }
}
