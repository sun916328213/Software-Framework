using System;
using Vision.Core;
using Vision.Core.ObservableCollectionEx;
using Vision.Core.Tools;
using Vision.Core.Vision;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Tools.Find;
using HalconDotNet;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

using System.Text.Json.Serialization;

namespace DPLibrary_Halcon_F48.Tools.FindLine
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum E_MeasureTransition 
    {
        [Description("任意极性")]
        all,
        [Description("暗→亮")]
        positive,
        [Description("亮→暗")]
        negative
    }

    public class MeasureParam : INotifyPropertyChanged
    {
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public MeasureParam() { }
        public MeasureParam(E_XLDType xldType)
        {
            XLDType = xldType;
        }

        protected bool mIsHit;
        protected string mName;

        public bool IsHit
        {
            get { return mIsHit; }
            set
            {
                if (mIsHit != value)
                {
                    mIsHit = value;
                    OnPropertyChanged(string.Empty);
                };
            }
        }

        public string Name
        {
            get { return mName; }
            set
            {
                if (mName != value)
                {
                    mName = value;
                    OnPropertyChanged(string.Empty);
                };
            }
        }

        // ⚠️ setter 保持非公有（面板本来就不让改类型，这是设计如此），但**必须加 [JsonInclude]**：
        // ① System.Text.Json 默认不给非公有 setter 赋值，而且是**静默跳过**（不抛异常）——
        //    JSON 里明明写着 "XLDType": "SectorCircle"，反序列化后却回退成枚举默认值
        //    E_XLDType.Null(0)，表现为「读档后卡尺类型丢失」。旧版 Newtonsoft.Json 能写
        //    private setter，所以迁到 STJ 之后才暴露（不是 STJ 的 bug，是契约差异）。
        //    [JsonInclude] 就是让 STJ 使用这个非公有 setter（private / protected 都实测有效）。
        // ② ⛔ **不要把 setter 改成 public** —— 那会让 SetMethod.IsPublic 变 true，
        //    PropertyEditorBindingHelper.IsEditableProperty 随之放行 ⇒ 面板把它从
        //    「灰色只读文本」变成可编辑下拉 ⇒ **改不了类型是刻意的，改成 public 是行为变更**。
        //    类型只在构造时确定（见本类构造函数；「功能新增卡尺」时选定）。
        [JsonInclude]
        [JsonPropertyName("XLDType")]
        [ToolPropertyAttribute(DisplayName = "卡尺类型", Description = "卡尺的类型",EditorType = E_PropertyEditorType.SingleSelect)]
        public E_XLDType XLDType
        { 
            get; 
            private set;
        }

        [JsonPropertyName("MeasureTransition")]
        [ToolPropertyAttribute(DisplayName = "卡尺极性", Description = "卡尺极性", EditorType = E_PropertyEditorType.SingleSelect)]
        public E_MeasureTransition MeasureTransition { get; set; } = E_MeasureTransition.all;

        [JsonPropertyName("IsFirstLine")]
        [ToolPropertyAttribute(DisplayName = "卡尺选择", Description = "获取的对象是否是第一个查找到的，否则为last", EditorType = E_PropertyEditorType.Checkbox)]
        public bool IsFirstLine { get; set; } = true;

        [ToolPropertyAttribute(DisplayName = "卡尺高度", Description = "卡尺高度",
                        EditorType = E_PropertyEditorType.Double, Min = 1, Max = 511)]
        public double Height { get; set; } = 20;

        [ToolPropertyAttribute(DisplayName = "卡尺宽度", Description = "卡尺宽度",
                        EditorType = E_PropertyEditorType.Double, Min = 1, Max = 511)]
        public double Width { get; set; } = 5;

        [ToolPropertyAttribute(DisplayName = "平滑标准差", Description = "平滑标准差",
                        EditorType = E_PropertyEditorType.Double, Min = 0.4, Max = 100)]
        public double Sigma { get; set; } = 1;

        [ToolPropertyAttribute(DisplayName = "对比度阈值", Description = "对比度阈值",
                EditorType = E_PropertyEditorType.Int, Min = 1, Max = 255)]
        public double Threshold { get; set; } = 10;

        [ToolPropertyAttribute(DisplayName = "卡尺点数", Description = "卡尺点数",
        EditorType = E_PropertyEditorType.Int, Min = 3)]
        public int PointsNumber { get; set; } = 20;

        [ToolPropertyAttribute(DisplayName = "忽略点数", Description = "忽略点数",
                EditorType = E_PropertyEditorType.Int, Min = 0)]
        public int LossNumber { get; set; } = 0;

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            return Name + "   " + IsHit;
        }
    }

    public class HTool_FindGroup : HVisionTool
    {
        public HTool_FindGroup() 
        {
            AttachSync();
        }

        public HTool_FindGroup(bool attachSync)
        {
            AttachSync();
        }

        PairedCollections<HXLD2, MeasureParam> sync;

        // 在反序列化完成 or 新建完成后调用
        public void AttachSync()
        {
            sync?.Dispose();
            sync = new PairedCollections<HXLD2, MeasureParam>(
                MeasurePaths, MeasureParams,
                makeRightFromLeft: s => new MeasureParam(s.XLDType) { Name = s.XLDName, IsHit = s.IsHit },
                makeLeftFromRight: n =>
                {
                    throw new Exception("无法转换到轮廓卡尺");
                },
                new[] { "XLDName", "IsHit" },
                new[] { "Name", "IsHit" }
            );
        }

        public override E_ToolType ToolType => E_ToolType.HTool_FindGroup;

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"FindGroup_{ToolMge.Instance.GetToolCount<HTool_FindGroup>() + 1}";


        [ToolPropertyAttribute(Group = "2.卡尺参数", DisplayName = "卡尺列表", Description = "索引对应与界面卡尺的实例",
        EditorType = E_PropertyEditorType.DataClassList, BindingProp = "Name",BindingIsHit = "IsHit")]
        public ObservableCollection<MeasureParam> MeasureParams { get; set; } = new ObservableCollection<MeasureParam>();

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

            HObject ho_ResultContours;
            HObject ho_UsedEdges, ho_Contour;

            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_Row = new HTuple(), hv_Column = new HTuple();
            HTuple hv_MetrologyHandle = new HTuple(), hv_LineParam = new HTuple();
            HTuple hv_LineIndex = new HTuple();
            HTuple hv_Row1 = new HTuple(), hv_Col1 = new HTuple();
            HTuple hv_UsedRow = new HTuple(), hv_UsedColumn = new HTuple();

            HTuple genValNames = new HTuple();
            HTuple genVals = new HTuple();

            HTuple datas = null;
            HTuple Sx = null, Sy = null, Phi = null, Theta = null, Tx = null, Ty = null;

            HOperatorSet.GenEmptyObj(out ho_ResultContours);
            HOperatorSet.GenEmptyObj(out ho_UsedEdges);
            HOperatorSet.GenEmptyObj(out ho_Contour);
            try
            {
                HOperatorSet.GetImageSize(mImage, out hv_Width, out hv_Height);
                //创建 metrology 模型
                hv_MetrologyHandle.Dispose();
                HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                HOperatorSet.SetMetrologyModelImageSize(hv_MetrologyHandle, hv_Width, hv_Height);

                //集合卡尺工具性能优化原因，会无视视觉域

                //获取矩阵
                HTuple matrixData = ModelMixtrue.GetMatrix().ToHMixtrue();

                //遍历卡尺列表，添加测量对象
                for (int i = 0; i < MeasurePaths.Count; i++)
                {
                    HXLD2 item = MeasurePaths[i];
                    switch (item.XLDType)//ROI类型必须是可旋转的矩形，否则无法进行直线查找
                    {
                        case E_XLDType.Line:
                            HXLD_Line rect = item as HXLD_Line;
                            //轴两端点
                            double RowA1 = rect.StartPoint.Y;
                            double ColA1 = rect.StartPoint.X;
                            double RowA2 = rect.EndPoint.Y;
                            double ColA2 = rect.EndPoint.X;

                            //定义直线的初始近似位置
                            //格式：[Row1, Col1, Row2, Col2]
                            hv_LineParam?.Dispose(); hv_Row1?.Dispose(); hv_Col1?.Dispose();
                            hv_LineParam = new HTuple();
                            //参考系需要逆转图像（关键！）
                            if (ModelMixtrue.ToString() != "[1, 0, 0, 0, 1, 0]")
                            {
                                HOperatorSet.AffineTransPoint2d(matrixData, new HTuple(RowA1, RowA2), new HTuple(ColA1, ColA2), out hv_Row1, out hv_Col1);
                                hv_LineParam = new HTuple(hv_Row1.DArr[0], hv_Col1.DArr[0], hv_Row1.DArr[1], hv_Col1.DArr[1]);
                            }
                            else
                            {
                                hv_LineParam = new HTuple(RowA1, ColA1, RowA2, ColA2);
                            }

                            //绘制卡尺检测线
                            HObject XLDS;
                            HOperatorSet.AffineTransContourXld(rect.GetXLD(), out XLDS, matrixData);
                            I_HXLD xld3 = new HXLD_Null(XLDS) { DispColor = E_VisionColor.Blue };
                            ResultXLDs.Add(xld3);

                            MeasureParam mp = MeasureParams[i];
                            //生成卡尺的控制参数键值对
                            genValNames?.Dispose(); genValNames = new HTuple();
                            genVals?.Dispose(); genVals = new HTuple();
                            //卡尺点数
                            genValNames = genValNames.TupleConcat("num_measures"); genVals = genVals.TupleConcat(mp.PointsNumber);
                            //卡尺极性  'positive' = 暗→亮；'negative' = 亮→暗；不确定先用 'all'
                            genValNames = genValNames.TupleConcat("measure_transition"); genVals = genVals.TupleConcat(mp.MeasureTransition.ToString());
                            //卡尺选择
                            genValNames = genValNames.TupleConcat("measure_select"); genVals = genVals.TupleConcat(mp.IsFirstLine ? "first" : "last");

                            //添加直线测量对象
                            hv_LineIndex.Dispose();
                            HOperatorSet.AddMetrologyObjectGeneric(
                                hv_MetrologyHandle,     //工具句柄
                                "line",                 //检测类型
                                hv_LineParam,           //工具的必须参数，线的话就是起点和终点坐标
                                mp.Height,                     //垂直检测线方向的长度
                                mp.Width,                      //检测线方向的宽度
                                mp.Sigma,                      //平滑标准差
                                mp.Threshold,                     //边缘幅度阈值
                                genValNames,         //更多的数据
                                genVals,
                                out hv_LineIndex);
                            break;
                        case E_XLDType.SectorCircle:
                            HXLD_SectorCircle circle = item as HXLD_SectorCircle;
                            //轴两端点
                            double centerX = circle.CenterPoint.X;
                            double centerY = circle.CenterPoint.Y;
                            double r = circle.Radius;
                            double sp = circle.StartAngle / 180.0 * Math.PI;
                            double ep = circle.EndAngle / 180.0 * Math.PI;

                            //定义直线的初始近似位置
                            //格式：[Row1, Col1, Row2, Col2]
                            hv_LineParam?.Dispose(); hv_Row1?.Dispose(); hv_Col1?.Dispose();
                            hv_LineParam = new HTuple();
                            //参考系需要逆转图像（关键！）
                            if (ModelMixtrue.ToString() != "[1, 0, 0, 0, 1, 0]")
                            {
                                HOperatorSet.AffineTransPoint2d(matrixData, centerY, centerX, out hv_Row1, out hv_Col1);
                                hv_LineParam = new HTuple(hv_Row1.D, hv_Col1.D, r);
                            }
                            else
                            {
                                hv_LineParam = new HTuple(centerY, centerX, r);
                            }

                            //绘制卡尺检测线
                            HObject XLDS2;
                            HOperatorSet.AffineTransContourXld(circle.GetXLD(), out XLDS2, matrixData);
                            I_HXLD xldc = new HXLD_Null(XLDS2) { DispColor = E_VisionColor.Blue };
                            ResultXLDs.Add(xldc);

                            MeasureParam mpc = MeasureParams[i];

                            HOperatorSet.HomMat2dToAffinePar(matrixData, out Sx, out Sy, out Phi, out Theta, out Tx, out Ty);

                            //生成卡尺的控制参数键值对
                            genValNames?.Dispose(); genValNames = new HTuple();
                            genVals?.Dispose(); genVals = new HTuple();
                            //卡尺点数
                            genValNames = genValNames.TupleConcat("num_measures"); genVals = genVals.TupleConcat(mpc.PointsNumber);
                            //卡尺极性  'positive' = 暗→亮；'negative' = 亮→暗；不确定先用 'all'
                            genValNames = genValNames.TupleConcat("measure_transition"); genVals = genVals.TupleConcat(mpc.MeasureTransition.ToString());
                            //卡尺选择
                            genValNames = genValNames.TupleConcat("measure_select"); genVals = genVals.TupleConcat(mpc.IsFirstLine ? "first" : "last");
                            //起始弧度
                            genValNames = genValNames.TupleConcat("start_phi"); genVals = genVals.TupleConcat(sp + Phi);
                            //结束弧度
                            genValNames = genValNames.TupleConcat("end_phi"); genVals = genVals.TupleConcat(ep + Phi);

                            //添加直线测量对象
                            hv_LineIndex.Dispose();
                            HOperatorSet.AddMetrologyObjectGeneric(
                                hv_MetrologyHandle,     //工具句柄
                                "circle",                 //检测类型
                                hv_LineParam,           //工具的必须参数，线的话就是起点和终点坐标
                                mpc.Height,                     //垂直检测线方向的长度
                                mpc.Width,                      //检测线方向的宽度
                                mpc.Sigma,                      //平滑标准差
                                mpc.Threshold,                     //边缘幅度阈值
                                genValNames,         //更多的数据（点数.）
                                genVals,
                                out hv_LineIndex);
                            break;
                        default:
                            break;
                    }
                }

                //执行测量（无视了视觉域）
                HOperatorSet.ApplyMetrologyModel(mImage, hv_MetrologyHandle);
                //结果轮廓
                ho_ResultContours.Dispose();
                HOperatorSet.GetMetrologyObjectResultContour(
                    out ho_ResultContours,
                    hv_MetrologyHandle,
                    "all",
                    "all",
                    1.5);
                ResultXLDs.Add(new HXLD_Null(new HXLDCont(ho_ResultContours)));

                //拆分结果数据
                for (int i = 0; i < MeasurePaths.Count; i++)
                {
                    try
                    {
                        datas?.Dispose(); datas = null;
                        HOperatorSet.GetMetrologyObjectResult(hv_MetrologyHandle, i, "all", "result_type",
                            "all_param", out datas);
                
                        switch (MeasureParams[i].XLDType)
                        {
                            case E_XLDType.Line:
                                //直线（"row_begin", "column_begin", "row_end", "column_end"）
                                Results.Add(new HFindLineResult()
                                {
                                    Name = MeasureParams[i].Name,
                                    IsFailed = false,
                                    StartPoint = new System.Windows.Point(Math.Round(datas[1].D,3), Math.Round(datas[0].D,3)),
                                    EndPoint = new System.Windows.Point(Math.Round(datas[3].D,3), Math.Round(datas[2].D, 3))
                                });

                                double r1 = datas[0].D;
                                double c1 = datas[1].D;
                                double r2 = datas[2].D;
                                double c2 = datas[3].D;
                                double dr = r2 - r1, dc = c2 - c1;
                                // 先构造一个“很长”的线段
                                const double M = 1e6;
                                double pr1 = r1 - M * dr, pc1 = c1 - M * dc;
                                double pr2 = r1 + M * dr, pc2 = c1 + M * dc;
                                if (LiangBarskyClip.Clip(0, hv_Width, 0, hv_Height, ref pc1, ref pr1, ref pc2, ref pr2))//获取区域内的线段的延长点
                                {
                                    ResultXLDs.Add(new HXLD_Null(new HXLDCont(new HTuple(pr1, pr2), new HTuple(pc1, pc2))));
                                }
                                break;
                            case E_XLDType.SectorCircle:

                                //圆（"row", "column", "radius"）
                                Results.Add(new HFindCircleResult()
                                {
                                    Name = MeasureParams[i].Name,
                                    IsFailed = false,
                                    CenterPoint = new System.Windows.Point(Math.Round(datas[1].D, 3), Math.Round(datas[0].D, 3)),
                                    R = Math.Round(datas[2].D,3)
                                });
                                break;
                            case E_XLDType.SectorEllipsoid:
                            case E_XLDType.Null:
                            default:
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        switch (MeasureParams[i].XLDType)
                        {
                            case E_XLDType.Line:
                                Results.Add(new HFindLineResult()
                                {
                                    Name = MeasureParams[i].Name,
                                    IsFailed = true
                                });
                                break;
                            case E_XLDType.SectorCircle:
                                Results.Add(new HFindCircleResult()
                                {
                                    Name = MeasureParams[i].Name,
                                    IsFailed = true
                                });
                                break;
                            case E_XLDType.SectorEllipsoid:
                            case E_XLDType.Null:
                            default:
                                break;
                        }
                    }
                }

                //显示结果(叉点)
                hv_UsedRow.Dispose();
                HOperatorSet.GetMetrologyObjectResult(hv_MetrologyHandle, "all", "all", "used_edges",
                    "row", out hv_UsedRow);
                hv_UsedColumn.Dispose();
                HOperatorSet.GetMetrologyObjectResult(hv_MetrologyHandle, "all", "all", "used_edges",
                    "column", out hv_UsedColumn);
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    ho_UsedEdges.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_UsedEdges, hv_UsedRow, hv_UsedColumn, 8, (new HTuple(45)).TupleRad());
                }
                I_HXLD xld = new HXLD_Null(ho_UsedEdges);
                ResultXLDs.Add(xld);
                //显示框，只做UI优化提示，无信息作用
                ho_Contour.Dispose(); hv_Row.Dispose(); hv_Column.Dispose();
                HOperatorSet.GetMetrologyObjectMeasures(out ho_Contour, hv_MetrologyHandle, "all",
                    "all", out hv_Row, out hv_Column);
                I_HXLD xld2 = new HXLD_Null(ho_Contour);
                ResultXLDs.Add(xld2);

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
                ho_ResultContours.Dispose();
                ho_UsedEdges.Dispose();
                ho_Contour.Dispose();

                genValNames?.Dispose(); 
                genVals?.Dispose(); 
                datas?.Dispose(); 
                Sx?.Dispose(); 
                Sy?.Dispose(); 
                Phi?.Dispose(); 
                Theta?.Dispose(); 
                Tx?.Dispose(); 
                Ty?.Dispose(); 

                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_Row.Dispose();
                hv_Column.Dispose();
                hv_MetrologyHandle.Dispose();
                hv_LineParam.Dispose();
                hv_LineIndex.Dispose();
                hv_Row1.Dispose();
                hv_Col1.Dispose();
                hv_UsedRow.Dispose();
                hv_UsedColumn.Dispose();
            }
        }

        protected override void DoOnDeserializing()
        {
            base.DoOnDeserializing();
            sync.Dispose();
            sync = null;
        }

        protected override void DoAfterDeserialized()
        {
            base.DoAfterDeserialized();
            AttachSync();
        }
    }
}
