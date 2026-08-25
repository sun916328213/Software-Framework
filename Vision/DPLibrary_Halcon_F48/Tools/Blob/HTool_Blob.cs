using DPLibrary_Base_F48;
using DPLibrary_Base_F48.ObservableCollectionEx;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Base_F48.Vision;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Base.DispMessage;
using DPLibrary_Halcon_F48.Tools.Find;
using DPLibrary_Halcon_F48.Tools.FindLine;
using HalconDotNet;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
 

using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DPLibrary_Halcon_F48.Tools.Blob
{


    public class Scope
    {
        public string Name { get; set; }
        public double Min { get; set; } = 0;


        public double Max { get; set; } = 0;

    }

    public class HTool_Blob : HVisionTool
    {


        public HTool_Blob()
        {

            SelectFeaturesTypes.IsSuspend = false;
            SelectFeaturesTypes.CollectionChanged += OnCollectionChanged;

        }

        public void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            int count = SelectFeaturesTypes.Count;
            scopes.Clear();
            for (int i = 0; i < count; i++)
            {
                var a = new Scope { Name = SelectFeaturesTypes[i].ToString(), Min = 0, Max = 99999 };

                scopes.Add(a);

            }
        }
 
        public override E_ToolType ToolType => E_ToolType.HTool_Blob;


        /// <summary>
        /// 工具名称
        /// </summary>
        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        public override string Name { get; set; } = $"Blob_{ToolMge.Instance.GetToolCount<HTool_Blob>() + 1}";


        /// <summary>
        /// 最小灰度值
        /// </summary>
        [ToolPropertyAttribute(Group = "2.Blob参数", DisplayName = "最小灰度值", Description = "MinGray",
        EditorType = E_PropertyEditorType.Int, Max = 255, Min = 0)]
        public int MinGray { get; set; } = 0;

        /// <summary>
        /// 最大灰度值
        /// </summary>
        [ToolPropertyAttribute(Group = "2.Blob参数", DisplayName = "最大灰度值", Description = "MaxGray",
        EditorType = E_PropertyEditorType.Int, Max = 255, Min = 0)]
        public int MaxGray { get; set; } = 255;

        /// <summary>
        /// 支持选择区域的参数类型
        /// </summary>
        [ToolPropertyAttribute(Group = "2.Blob参数", DisplayName = "支持选择区域的参数类型", Description = "用于支持区域的类型列表",
        EditorType = E_PropertyEditorType.MultiSelect)]
        public BulkObservableCollection<E_FeaturesType> SelectFeaturesTypes { get; set; } = new BulkObservableCollection<E_FeaturesType>() { E_FeaturesType.area };

        /// <summary>
        /// 参数范围
        /// </summary>
        [ToolPropertyAttribute(Group = "2.Blob参数", DisplayName = "参数范围", Description = "参数范围",
       EditorType = E_PropertyEditorType.DataGridList)]
        public ObservableCollection<Scope> scopes { get; set; } = new ObservableCollection<Scope>() { new Scope { Name = "area", Min = 0, Max = 99999 } };


        /// <summary>
        /// 参数关系
        /// </summary>
        [ToolPropertyAttribute(DisplayName = "参数关系", Group = "2.Blob参数", Description = "上列选择参数之间关系", EditorType = E_PropertyEditorType.SingleSelect)]
        public E_OpearationType E_Opearation { get; set; }




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
            HObject gRegions = null;

            HObject tRegions = null;
            HObject cRegions = null;
            HObject sRegions = null;
            HObject fRegions = null;
            HTuple _Area = null, _Row = null, _Column = null;

            HObject temp = null;
            HObject roiImage = null;
            try
            {
                if (SearchRegions.Count > 0)//有ROI
                {
                    hRegions = SearchRegions[0].GetRegion().Clone();

                    for (global::System.Int32 i = 1; i < SearchRegions.Count; i++)
                    {
                        HOperatorSet.Union2(hRegions, SearchRegions[i].GetRegion(), out temp);
                        hRegions.Dispose(); // 释放上一次的合并结果，避免内存泄漏
                        hRegions = temp;
                    }
                    //获取矩阵
                    HTuple matrixData = ModelMixtrue.GetMatrix().ToHMixtrue();
                    if (ModelMixtrue.ToString() != "[1, 0, 0, 0, 1, 0]")
                    {
                        HOperatorSet.AffineTransRegion(hRegions, out gRegions, matrixData, "nearest_neighbor");
                    }
                    else
                    {
                        gRegions = hRegions;
                    }
                    HOperatorSet.ReduceDomain(mImage, gRegions, out roiImage);
                    HOperatorSet.Threshold(roiImage, out tRegions, MinGray, MaxGray);
                    HOperatorSet.Connection(tRegions, out cRegions);
                    HOperatorSet.FillUp(cRegions, out fRegions);

                    HTuple Features = new HTuple();
                    HTuple min = new HTuple();
                    HTuple max = new HTuple();
                    for (int j = 0; j < SelectFeaturesTypes.Count; j++)
                    {
                        Features.Append(SelectFeaturesTypes[j].ToString());
                        min.Append(scopes[j].Min);
                        max.Append(scopes[j].Max);
                    }
                    HOperatorSet.SelectShape(fRegions, out sRegions, Features, new HTuple(E_Opearation.ToString()), min, max);
                    HOperatorSet.AreaCenter(sRegions, out _Area, out _Row, out _Column);

                }
                else
                {

                    HOperatorSet.Threshold(mImage, out tRegions, MinGray, MaxGray);
                    HOperatorSet.Connection(tRegions, out cRegions);
                    HOperatorSet.FillUp(cRegions, out fRegions);

                    HTuple Features = new HTuple();

                    HTuple min = new HTuple();
                    HTuple max = new HTuple();
                    for (int j = 0; j < SelectFeaturesTypes.Count; j++)
                    {
                        Features.Append(SelectFeaturesTypes[j].ToString());
                        min.Append(scopes[j].Min);
                        max.Append(scopes[j].Max);
                    }
                    HOperatorSet.SelectShape(fRegions, out sRegions, Features, new HTuple(E_Opearation.ToString()), min, max);
                    HOperatorSet.AreaCenter(sRegions, out _Area, out _Row, out _Column);


                }
                for (int i = 0; i < sRegions.CountObj(); i++)
                {
                    HROI_Null region = new HROI_Null(sRegions.SelectObj(i + 1))
                    {
                        DispColor = DispRegionColor,
                        IsFull = IsDispRegionFull
                    };
                    HTuple value = new HTuple();
                    HTuple featuresHelper = new HTuple();
                    HTuple features1 = new HTuple();
                    HTuple values1 = new HTuple();

                    List<E_FeaturesDispType> aa = Enum.GetValues(typeof(E_FeaturesDispType)).Cast<E_FeaturesDispType>().ToList();
                    foreach (var item in aa)
                    {
                        featuresHelper.Append(new HTuple(item.ToString()));
                    }
                    features1 = featuresHelper.TupleInsert(new HTuple(3), new HTuple("\n"));

                    HOperatorSet.RegionFeatures(sRegions.SelectObj(i + 1), featuresHelper, out value);
                    ResultRegions.Add(region);

                    values1 = (value.TupleString(".3f")).TupleInsert(new HTuple(3), new HTuple("\n"));

                    DispMessages.Add(new HDispMessage()
                    {
                        Anchor = DispAnchor.Center,
                        UL = new System.Windows.Point(Math.Round(_Column.TupleSelect(i).D, 3),
                                                             Math.Round(_Row.TupleSelect(i).D, 3)),
                        Message = (i + 1).ToString()
                    });
                    Results.Add(new HBlobResult()
                    {
                        ResultRegion = region,
                        Name = (i + 1).ToString(),
                        Area = $"{Math.Round(_Area.TupleSelect(i).D, 3)}",
                        Center = new System.Windows.Point(Math.Round(_Column.TupleSelect(i).D, 3),
                                                             Math.Round(_Row.TupleSelect(i).D, 3)),
                        features = features1.TupleInsert(new HTuple(6), new HTuple("\n")),
                        values = values1.TupleInsert(new HTuple(6), new HTuple("\n"))


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
                gRegions?.Dispose(); gRegions = null;

                temp?.Dispose(); temp = null;
                roiImage?.Dispose(); roiImage = null;
                tRegions?.Dispose(); tRegions = null;
                cRegions?.Dispose(); cRegions = null;
                sRegions?.Dispose(); sRegions = null;
                fRegions?.Dispose(); fRegions = null;
                _Area?.Dispose(); _Area = null;
                _Row?.Dispose(); _Row = null;
                _Column?.Dispose(); _Column = null;
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


        protected override void DoOnDeserializing()
        {
            base.DoOnDeserializing();

        }

        protected override void DoAfterDeserialized()
        {
            base.DoAfterDeserialized();

        }
    }
}
