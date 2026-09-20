using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Base.DispMessage;
using DPLibrary_Halcon_F48.Tool;
using HalconDotNet;
using System.Data.Common;
using System.Diagnostics;

using System.Text.Json.Serialization;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools
{
    /// <summary>
    /// 用于条形码读取的工具
    /// </summary>
    public class HTool_BarCode : Base_HCodeReader
    {
        public HTool_BarCode()
        {
            HOperatorSet.CreateBarCodeModel(new HTuple(), new HTuple(), out mBarcodeHandle);
            IsPersistence=true;
            
            // 只在集合为空时添加默认值
            if (SelectCodeTypes.Count == 0)
            {
                SelectCodeTypes.Add(E_BarCodeType.Auto);
            }
        }

        public override E_ToolType ToolType { get { return E_ToolType.HTool_BarCode; } }

        [JsonIgnore]
        protected HObjectModel3D mModel; // 可选，用于训练模型
        [JsonIgnore]
        protected HTuple mBarcodeHandle;

        protected int mCodesMaxNumber;

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"BarCode_{ToolMge.Instance.GetToolCount<HTool_BarCode>() + 1}";

        /// <summary>
        /// 支持条码的类型列表
        /// </summary>
        [ToolPropertyAttribute(DisplayName = "支持条码的类型", Group = "1.基础", Description = "用于支持条码的类型列表", EditorType = E_PropertyEditorType.MultiSelect)]
        public BulkObservableCollection<E_BarCodeType> SelectCodeTypes { get; set; } = new BulkObservableCollection<E_BarCodeType>() { E_BarCodeType .Auto};

        [ToolPropertyAttribute(Group = "1.基础", DisplayName = "过程数据", Description = "是否保留过程化数据", IsDisplay =false,
                       EditorType = E_PropertyEditorType.Checkbox)]
        /// <summary>
        /// 是否保留过程化数据
        /// </summary>
        public bool? IsPersistence
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "persistence", out v);
                bool m = v.I == 1 ? true : false;
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "persistence", (bool)value ? 1 : 0);
                OnPropertyChanged(nameof(IsPersistence));
            }
        } 

        [ToolPropertyAttribute(Group = "2.约束", DisplayName = "最小对比度",  Description = "条码最小对比度 (对比度不足可能导致无法识别，适当降低该值可增加容错)",
                               EditorType = E_PropertyEditorType.Double, Max = 1000000, Min = -1, Step = 0.1)]
        /// <summary>
        /// 条码最小对比度 (对比度不足可能导致无法识别，适当降低该值可增加容错)
        /// </summary>
        public double MinContrast
        {
            get
            {
                HTuple contrastMin;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "contrast_min", out contrastMin);
                double m = contrastMin.D;
                contrastMin.Dispose();
                return m;
            }
            set
            {
                HTuple val = new HTuple(value);
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "contrast_min", val);
                OnPropertyChanged(nameof(MinContrast));            
            }
        }

        [ToolPropertyAttribute(Group = "2.约束", DisplayName = "条码可变", Description = "条码元素(条/空)宽度的可变(畸变或压缩图像时可增加鲁棒性)",
                               EditorType = E_PropertyEditorType.Checkbox)]
        /// <summary>
        /// 条码元素（条/空）宽度的可变范围 (畸变或压缩图像时可增加鲁棒性)
        /// </summary>
        public bool? ElementSizeVar
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "element_size_variable", out v);
                bool m = v.ToString() == "\"true\"" ? true : false ;
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "element_size_variable", value.ToString().ToLower());
                OnPropertyChanged(nameof(ElementSizeVar));
            }
        }

        [ToolPropertyAttribute(Group = "2.约束", DisplayName = "最小元素尺寸", Description = "最小元素尺寸,是指条码中最窄的黑条或空白的宽度（以像素为单位）",
                               EditorType = E_PropertyEditorType.Double, Max = 64, Min = 0.6, Step = 0.1)]
        /// <summary>
        /// 最小元素尺寸,是指条码中最窄的黑条或空白的宽度（以像素为单位）
        /// </summary>
        public double ElementMinSize
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "element_size_min", out v);
                double m = v.D;
                v.Dispose();
                return m;
            }
            set
            {
                double val;
                if (value > 64)
                {
                    val = 64;
                }
                else if (value < 0.6)
                {
                    val = 0.6;
                }
                else
                {
                    val = value;
                }
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "element_size_min", value);
                OnPropertyChanged(nameof(ElementMinSize));
            }
        }

        [ToolPropertyAttribute(Group = "2.约束", DisplayName = "最大元素尺寸", Description = "最大元素尺寸,最窄元素的最大允许宽度。设太大会导致相邻条码粘连成一个；设太小可能找不到整个条码",
                               EditorType = E_PropertyEditorType.Double, Max = 256, Min = 1, Step = 0.1)]
        /// <summary>
        /// 最大元素尺寸,最窄元素的最大允许宽度。设太大会导致相邻条码粘连成一个；设太小可能找不到整个条码
        /// </summary>
        public double ElementMaxSize 
        { 
            get 
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "element_size_max", out v);
                double m = v.D ;
                v.Dispose();
                return m;
            }
            set
            {
                double val;
                if (value > 256)
                {
                    val = 256;
                }
                else if (value < 1)
                {
                    val = 1;
                }
                else
                {
                    val = value;
                }
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "element_size_max", value);
                OnPropertyChanged(nameof(ElementMaxSize));
            }
        }

        [ToolPropertyAttribute(Group = "2.约束", DisplayName = "最小条码高度", Description = "最小条码高度,-1 表示自动推算。高度很低（如 16 像素以下）时要手动设置，否则可能跳过检测",
                               EditorType = E_PropertyEditorType.Int, Min = -1)]
        /// <summary>
        /// 最小条码高度,-1 表示自动推算。高度很低（如 16 像素以下）时要手动设置，否则可能跳过检测
        /// </summary>
        public int HeightMin
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "barcode_height_min", out v);
                int m = v.I;
                v.Dispose();
                return m;
            }
            set
            {
                int val;
                if (value < 8)
                {
                    val = -1;
                }
                else
                {
                    val = value;
                }
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "barcode_height_min", value);
                OnPropertyChanged(nameof(HeightMin));
            }
        }

        [ToolPropertyAttribute(Group = "2.约束", DisplayName = "最小条码宽度", Description = "最小条码宽度,设定可提高速度与避免误检（特别是相邻条码粘连）",
                               EditorType = E_PropertyEditorType.Int, Min = -1)]
        /// <summary>
        /// 最小条码宽度,设定可提高速度与避免误检（特别是相邻条码粘连）
        /// </summary>
        public int WidthMin
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "barcode_width_min", out v);
                int m = v.I;
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "barcode_width_min", value);
                OnPropertyChanged(nameof(WidthMin));
            }
        }

        [ToolPropertyAttribute(Group = "2.约束", DisplayName = "最大条码宽度", Description = "最大条码宽度,设定可提高速度与避免误检（特别是相邻条码粘连）",
                               EditorType = E_PropertyEditorType.Int, Min = -1)]
        /// <summary>
        /// 最大条码宽度,设定可提高速度与避免误检（特别是相邻条码粘连）
        /// </summary>
        public int? WidthMax
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "barcode_width_max", out v);
                int m = v.I;
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "barcode_width_max", value);
                OnPropertyChanged(nameof(WidthMax));
            }
        }

        [ToolPropertyAttribute(Group = "3.检测参数", DisplayName = "扫描线数量", Description = "条码识别时使用的扫描线数量",
                               EditorType = E_PropertyEditorType.Int, Min = -1)]
        /// <summary>
        /// 条码识别时使用的扫描线数量
        /// </summary>
        public int? NumScanLines
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "num_scanlines", out v);
                int m = v.I;
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "num_scanlines", value);
                OnPropertyChanged(nameof(NumScanLines));
            }
        }

        [ToolPropertyAttribute(Group = "3.检测参数", DisplayName = "多票计数", Description = "是否使用多次测量，取频率最高的结果作为最终结果",
                               EditorType = E_PropertyEditorType.Checkbox)]
        /// <summary>
        /// 是否使用多次测量，取频率最高的结果作为最终结果
        /// </summary>
        public bool? IsMajorityVote
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "majority_voting", out v);
                bool m = v.ToString() == "\"true\"" ? true : false;
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "majority_voting", (bool)value ? "true" : "false");
                OnPropertyChanged(nameof(IsMajorityVote));
            }
        }

        [ToolPropertyAttribute(Group = "3.检测参数", DisplayName = "静区", Description = "条码四周静区（空白区域）的最小宽度，单位为条码宽度的倍数",
                               EditorType = E_PropertyEditorType.SingleSelect)]
        /// <summary>
        /// 条码四周静区（空白区域）的最小宽度，单位为条码宽度的倍数
        /// </summary>
        public E_QuietZoneType QuietZone
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "quiet_zone", out v);
                E_QuietZoneType m = (E_QuietZoneType)Enum.Parse(typeof(E_QuietZoneType), "QZ_" + v.ToString().Replace("\"",""));
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "quiet_zone", value.ToString().Replace("QZ_",""));
                OnPropertyChanged(nameof(QuietZone));
            }
        }

        [ToolPropertyAttribute(Group = "3.检测参数", DisplayName = "角度范围", Description = "角度范围 -180 - 180",
                               EditorType = E_PropertyEditorType.Double, Max = 180, Min = -180)]
        /// <summary>
        /// 角度范围
        /// </summary>
        public double Orientation
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "orientation", out v);
                double m = v.D;
                v.Dispose();
                return m;
            }
            set
            {
                double val;
                if (value > 180)
                {
                    val = 180;
                }
                else if (value < -180) 
                {
                    val = -180;
                }
                else
                {
                    val = value;
                }
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "orientation", val);
                OnPropertyChanged(nameof(Orientation));
            }
        }

        [ToolPropertyAttribute(Group = "3.检测参数", DisplayName = "小元素增强", Description = "小元素增强，element_size_min < 2.0 且非堆叠码",
                       EditorType = E_PropertyEditorType.Checkbox)]
        /// <summary>
        /// 小元素增强，element_size_min < 2.0 且非堆叠码
        /// </summary>
        public bool? IsSmallElementsRobustness
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "small_elements_robustness", out v);
                bool m = v.ToString() == "\"true\"" ? true : false;
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "small_elements_robustness", (bool)value ? "true" : "false");
                OnPropertyChanged(nameof(IsSmallElementsRobustness));
            }
        }
 
        [ToolPropertyAttribute(Group = "4.结果约束", DisplayName = "条码个数", Description = "最多识别多少个条码",
                               EditorType = E_PropertyEditorType.Int, Min = 0)]
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
            } }

        [ToolPropertyAttribute(Group = "4.结果约束", DisplayName = "条码长度", Description = "条码最长的字符长度",
                               EditorType = E_PropertyEditorType.Int, Min = 1)]
        /// <summary>
        /// 条码最长的字符长度
        /// </summary>
        public int? CodeMaxLength
        {
            get
            {
                HTuple v;
                HOperatorSet.GetBarCodeParam(mBarcodeHandle, "max_code_length", out v);
                int m = v.I;
                v.Dispose();
                return m;
            }
            set
            {
                HOperatorSet.SetBarCodeParam(mBarcodeHandle, "max_code_length", value);
                OnPropertyChanged(nameof(CodeMaxLength));
            }
        }


        Stopwatch sw = new Stopwatch();


        public override void Run()
        {
            //开始计时
            sw.Restart();
            RunMessage = "";
            ToolState = E_ToolState.Running;

            HObject decodedRegions = null;
            HTuple decodedData = null;
            HTuple decodeTypes = new HTuple();
            HTuple decodedTypes = null;
            HTuple decodedR = null;

            HObject hRegions = null;
            HObject temp = null;
            HObject roiImage = null;

            HTuple area = null, row = null, col = null;
            HTuple decodedQuality = null;


            Results.Clear();
            ResultRegions.Clear();
            DispMessages.Clear();
            ResultXLDs.Clear();

            try
            {
                for (int i = 0; i < SelectCodeTypes.Count; i++)
                {
                    decodeTypes[i] = SelectCodeTypes[i].ToHalconString();
                }

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
                    HOperatorSet.FindBarCode(
                        roiImage,
                        out decodedRegions,
                        mBarcodeHandle,
                        decodeTypes,  // 关键：指定仅识别UPC-E码
                        out decodedData
                    );
                }
                else//全检
                {
                    HOperatorSet.FindBarCode(
                        mImage,
                        out decodedRegions,
                        mBarcodeHandle,
                        decodeTypes,  // 关键：指定仅识别UPC-E码
                        out decodedData
                    );
                }

                HOperatorSet.GetBarCodeResult(mBarcodeHandle, "all", "decoded_types", out decodedTypes);
                HOperatorSet.GetBarCodeResult(mBarcodeHandle, "all", "orientation", out decodedR);

                // 创建临时列表存储识别结果
                var tempResults = new List<(HROI_Null region, string type, string code, Point center, double r, double quality)>();

                for (int i = 0; i < decodedData.Length; i++)
                {
                    HOperatorSet.GetBarCodeResult(mBarcodeHandle, i, "quality_isoiec15416", out decodedQuality);

                    HROI_Null region = new HROI_Null(decodedRegions.SelectObj(i + 1))
                    {
                        DispColor = DispRegionColor,
                        IsFull = IsDispRegionFull
                    };

                    area?.Dispose();
                    row?.Dispose();
                    col?.Dispose();

                    HOperatorSet.AreaCenter(region.GetRegion(), out area, out row, out col);
                    
                    // 存储到临时列表
                    tempResults.Add((
                        region,
                        decodedTypes[i],
                        decodedData[i],
                        new System.Windows.Point(Math.Round(col.D, 3), Math.Round(row.D, 3)),
                        Math.Round(decodedR[i].D, 3),
                        decodedQuality[0]
                    ));
                }

                // 按旋转后的坐标排序
                var sortedResults =Sort. SortByRotatedCoordinates(tempResults);

                // 按排序后的顺序添加到结果集合
                foreach (var item in sortedResults)
                {

                    //获取条码最大旋转外接矩形
                    HOperatorSet.SmallestRectangle1(item.region.GetRegion(), out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);

                    double width = col2.D - col1.D  ;
                    double height = row2.D - row1.D ;

                    Results.Add(new HCodeResult()
                    {
                        ResultRegion = item.region,
                        Type = item.type,
                        Code = item.code,
                        Center = item.center,
                        R = item.r,
                        Quality = item.quality,
                        Height= height,
                        Width = width
                    });

                    ResultRegions.Add(item.region);
                    DispMessages.Add(new HDispMessage()
                    {
                        Anchor = DispAnchor.Center,
                        UL = item.center,
                        Message = item.code
                    });
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
                hRegions?.Dispose(); hRegions = null;
                temp?.Dispose(); temp = null;
                roiImage?.Dispose(); roiImage = null;

                decodedRegions?.Dispose();
                decodedRegions = null;
                decodedData?.Dispose();
                decodedData = null;
                decodedTypes?.Dispose();
                decodedTypes = null;
                decodeTypes?.Dispose();
                decodeTypes = null;
                decodedR?.Dispose();
                decodedR = null;

                decodedQuality?.Dispose(); decodedQuality = null;

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

        public override void Config() 
        {
            
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
            base.DoOnDeserializing();
            SelectCodeTypes = null;//new BulkObservableCollection<E_BarCodeType>();
        }

      
    }
}
