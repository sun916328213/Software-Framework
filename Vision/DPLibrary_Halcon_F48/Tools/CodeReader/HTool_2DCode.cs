using Vision.Core;
using Vision.Core.Tools;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Base.DispMessage;
using HalconDotNet;
using System.Diagnostics;

using System.Text.Json.Serialization;

namespace DPLibrary_Halcon_F48.Tools
{
    /// <summary>
    /// 用于二维码读取的工具
    /// </summary>
    public class HTool_2DCode: Base_HCodeReader
    {
        public HTool_2DCode()
        {
            SelectCodeTypes.CollectionChanged += (s, e) => Update2DCodeHandles();
            Update2DCodeHandles();
        }

        public HTool_2DCode(E_2DCodeType ct)
        {
            SelectCodeTypes.Clear();
            SelectCodeTypes.Add(ct);
            SelectCodeTypes.CollectionChanged += (s, e) => Update2DCodeHandles();
            Update2DCodeHandles();
        }

        public override E_ToolType ToolType { get { return E_ToolType.HTool_2DCode; } }

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"2DCode_{ToolMge.Instance.GetToolCount<HTool_2DCode>() + 1}";

        //二维码不支持单模型不同码制，多码制的时候需要创建多模型
        [JsonIgnore]
        protected Dictionary<string,HTuple> m2DCodeHandles = new Dictionary<string, HTuple>();

        protected int mCodesMaxNumber = 1;

        /// <summary>
        /// 支持条码的类型列表
        /// </summary>
        [ToolPropertyAttribute(DisplayName = "支持条码的类型", Group = "1.基础", Description = "用于支持条码的类型列表", EditorType = E_PropertyEditorType.MultiSelect)]
        public BulkObservableCollection<E_2DCodeType> SelectCodeTypes { get; set; } = new BulkObservableCollection<E_2DCodeType>() { E_2DCodeType.QRCode };

        [ToolPropertyAttribute(DisplayName = "条码个数", Group = "4.结果约束", Description = "最多识别多少个条码", 
            EditorType = E_PropertyEditorType.Int ,Min = 1)]
        /// <summary>
        /// 最多识别多少个条码
        /// </summary>
        public int CodesMaxNumber
        {
            get { return mCodesMaxNumber; }
            set
            {
                if (mCodesMaxNumber != value)
                {
                    mCodesMaxNumber = value;
                    OnPropertyChanged(nameof(CodesMaxNumber));
                }
            }
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

            HObject symbolXLDs = null;
            HTuple resultHandles = null, decodedDataStrings = null;
            HObject hRegions = null;
            HObject temp = null;
            HObject roiImage = null;
            HObject region = null;
            HTuple area = null, row = null, col = null;

            try
            {
                foreach (var item in m2DCodeHandles)
                {
                    hRegions?.Dispose();
                    temp?.Dispose();
                    roiImage?.Dispose();
                    region?.Dispose();

                    if (SearchRegions.Count > 0)//有ROI
                    {
                        hRegions = SearchRegions[0].GetRegion().Clone();

                        for (global::System.Int32 i = 1; i < SearchRegions.Count; i++)
                        {
                            HOperatorSet.Union2(hRegions, SearchRegions[i].GetRegion(), out temp);
                            hRegions.Dispose(); // 释放上一次的合并结果，避免内存泄漏
                            hRegions = temp;
                        }

                        HOperatorSet.ReduceDomain(mImage, hRegions, out roiImage);
                        HOperatorSet.FindDataCode2d(
                                                roiImage,
                                                out symbolXLDs,
                                                item.Value,
                                                "stop_after_result_num",
                                                mCodesMaxNumber,
                                                out resultHandles, out decodedDataStrings);
                    }
                    else
                    {
                        HOperatorSet.FindDataCode2d(mImage,
                                                out symbolXLDs,
                                                item.Value,
                                                "stop_after_result_num",
                                                mCodesMaxNumber,
                                                out resultHandles, out decodedDataStrings);
                    }

 
                    // 解析结果
                    if (decodedDataStrings.Length > 0)
                    {
                        for (int i = 0; i < decodedDataStrings.Length; i++)
                        {
                            HOperatorSet.GenRegionContourXld(symbolXLDs.SelectObj(i + 1), out region, "filled");

                            HROI_Null resultRegion = new HROI_Null(region)
                            {
                                DispColor = DispRegionColor,
                                IsFull = IsDispRegionFull
                            };
                            ResultRegions.Add(resultRegion);

                            area?.Dispose();
                            row?.Dispose();
                            col?.Dispose();
                            HOperatorSet.AreaCenter(ResultRegions[i].GetRegion(), out area, out row, out col);
                            DispMessages.Add(new HDispMessage()
                            {
                                Anchor = DispAnchor.Center,
                                UL = new System.Windows.Point(Math.Round(col.D, 3), Math.Round(row.D, 3)),
                                Message = $"{decodedDataStrings[i].S}"
                            });
                            
                            //获取二维码的角度
                            HTuple angle = null;
                            HOperatorSet.GetDataCode2dResults(item.Value, i, "orientation", out angle);
                            HCodeResult r = new HCodeResult()
                            {
                                Center = new System.Windows.Point(Math.Round(col.D, 3), Math.Round(row.D, 3)),
                                Code = decodedDataStrings[i].S,
                                ResultRegion = resultRegion,
                                Type = item.Key,
                                R = Math.Round(angle.D,3)
                            };
                            Results.Add(r);
                        }

                        if (!DispModels.Contains(DisplayModel.Result))
                        {
                            DispModels.Add(DisplayModel.Result);
                        }

                        DispLayer = (DisplayLayers)DisplayModel.Result;

                        //找到则直接推出大循环（放置无效的查找）
                        break;
                    }
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
                symbolXLDs?.Dispose(); symbolXLDs = null;
                resultHandles?.Dispose(); resultHandles = null;
                decodedDataStrings?.Dispose(); decodedDataStrings = null;
                hRegions?.Dispose(); hRegions = null;
                temp?.Dispose(); temp = null;
                roiImage?.Dispose(); roiImage = null;

                region?.Dispose(); region = null;
                area?.Dispose(); area = null;
                row?.Dispose(); row = null;
                col?.Dispose(); col = null;
            }
        }

        public override void Save()
        {
            try
            {
                base.Save();
            }
            catch (Exception)
            {
                Console.WriteLine($"数据已保存到文件失败");
            }
        }

        public override void Config(   )
        {
        }

        private void Update2DCodeHandles()
        {
            // 更新 m2DCodeHandles 字典
            foreach (var item in SelectCodeTypes)
            {
                HTuple model;
                string modelName = item.ToHalconString();
                if (!m2DCodeHandles.ContainsKey(modelName))
                {
                    HOperatorSet.CreateDataCode2dModel(modelName, new HTuple(), new HTuple(), out model);
                    m2DCodeHandles.Add(modelName, model);
                }
            }
        }

        // 反序列化完后，用参数重建运行期对象
        protected override void DoAfterDeserialized()
        {
            base.DoAfterDeserialized();
            Update2DCodeHandles();
        }

        public override void Reset()
        {
            throw new NotImplementedException();
        }

        public override void Pause()
        {
            throw new NotImplementedException();
        }

        protected override void DoOnDeserializing()
        {
            SelectCodeTypes.Clear();
        }
    }
}
