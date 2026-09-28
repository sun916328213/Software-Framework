using Vision.Core;
using Vision.Core.Tools;
using DPLibrary_Halcon_F48.Base;
using HalconDotNet;
using System.Collections.ObjectModel;
using System.Diagnostics;


namespace DPLibrary_Halcon_F48.Tools.Geometry
{

    public class TypesList
    {
        public string 名称 { get; set; }
        public string 类型 { get; set; }
        public System.Windows.Point 直线起始点 { get; set; }
        public System.Windows.Point 直线终点 { get; set; }

        public System.Windows.Point 点or圆心坐标 { get; set; }
        public double 圆半径 { get; set; }
        public HXLD XLD { get; set; }   
        public HRegion Region { get; set; }

    }


    public class HTool_Geometry : HVisionTool
    {
        public HTool_Geometry()
        {



        }
        public override E_ToolType ToolType => E_ToolType.HTool_Geometry;
        [ToolPropertyAttribute(DisplayName = "几何工具类型", Group = "2.Geometry参数", Description = "用于支持几何工具类型列表", EditorType = E_PropertyEditorType.SingleSelect)]
        public E_GeometryType GeometryType { get; set; }
        [ToolPropertyAttribute(Group = "2.Geometry参数", DisplayName = "类型集合", Description = "所有可用于几何工具计算的类型集合",
        EditorType = E_PropertyEditorType.DataGridList)]
        public ObservableCollection<TypesList> typesList { get; set; } = new ObservableCollection<TypesList>() {
            new TypesList { 名称 = "Line1", 类型 = "Line", 直线起始点 = new System.Windows.Point(1640.336,928.764),直线终点= new System.Windows.Point(1647.757,1099.437) },
            new TypesList { 名称 = "Line2", 类型 = "Line", 直线起始点 = new System.Windows.Point(1356.473,1125.409),直线终点= new System.Windows.Point(1289.682,1129.12) },
            new TypesList { 名称 = "Point1", 类型 = "Point", 点or圆心坐标= new System.Windows.Point(1409.412,812.869) },
            new TypesList { 名称 = "Circle1", 类型 = "Circle", 点or圆心坐标= new System.Windows.Point(1409.412,812.869),圆半径=58.813 },
            new TypesList { 名称 = "XLD1", 类型 = "XLD", XLD=new HXLD() },
            new TypesList { 名称 = "Region1", 类型 = "Region", Region=new HRegion() },


        };
        



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
            HTuple row1=null, row2=null;
            HTuple column1=null, column2=null;
            HTuple row3 = null, row4 = null;
            HTuple column3 = null, column4 = null;
            HTuple centerPointrow = null, centerPointcolumn = null,R=null,Distance=null;
            HTuple Irow=null, Icolumn=null;
            HTuple IsParallel = null, Angle = null, Degree = null, Distancemin = null, Distancemax = null;
            HTuple hv_k= null,hv_b=null, hv_xTest=null, hv_yTest=null;   
            HObject hv_Intersection=null,Line1=null,Line2=null;
            
            try
            {
                switch(GeometryType)
                {
                    case E_GeometryType.LineLine:
                        {
                            row1 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线起始点.Y);
                            column1 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线起始点.X);
                            row2 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线终点.Y);
                            column2 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线终点.X);
                            row3 = new HTuple(typesList.Where(u => u.名称 == "Line2").ToList()[0].直线起始点.Y);
                            column3 = new HTuple(typesList.Where(u => u.名称 == "Line2").ToList()[0].直线起始点.X);
                            row4 = new HTuple(typesList.Where(u => u.名称 == "Line2").ToList()[0].直线终点.Y);
                            column4 = new HTuple(typesList.Where(u => u.名称 == "Line2").ToList()[0].直线终点.X);
                            HOperatorSet.IntersectionLl(row1, column1, row2, column2, row3, column3, row4, column4, out Irow, out Icolumn, out IsParallel);
                            if (IsParallel.I==1)//交点坐标无效
                            {
                                Irow = new HTuple(0);
                                Icolumn = new HTuple(0);
                                Degree=new HTuple(0);
                                HOperatorSet.DistanceSs(row1, column1, row2, column2, row3, column3, row4, column4, out Distancemin,out Distancemax);

                            }
                            else
                            {
                                HOperatorSet.AngleLl(row1, column1, row2, column2, row3, column3, row4,column4,out Angle);
                                Degree = Math.Round(Angle.D, 3) * 180 / Math.PI;
                                Distancemin = new HTuple(0);
                                Distancemax=new HTuple(0);
                            }
                            hv_Intersection?.Dispose();
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                HOperatorSet.GenContourPolygonXld(out Line1, new HTuple(row1, column1), new HTuple(row2, column2));
                                HOperatorSet.GenContourPolygonXld(out Line2, new HTuple(row3, column3), new HTuple(row4, column4));

                                HOperatorSet.GenCrossContourXld(out hv_Intersection, Irow, Icolumn, 20, (new HTuple(45)).TupleRad());
                            }
                            Results.Add(new HLLResult()
                            {
                                Name = "线线工具",

                                IsFailed = true,
                                直线交点 = new System.Windows.Point(Math.Round(Irow.D, 3), Math.Round(Icolumn.D, 3)),
                                直线夹角 = Degree,
                                最小距离 = Math.Round(Distancemin.D, 3),
                                最大距离 = Math.Round(Distancemax.D, 3),

                            });
                            I_HXLD xld = new HXLD_Null(hv_Intersection);
                            I_HXLD xld1 = new HXLD_Null(Line1);
                            I_HXLD xld2 = new HXLD_Null(Line2);

                            ResultXLDs.Add(xld);
                            ResultXLDs.Add(xld1);
                            ResultXLDs.Add(xld2);
                            if (!DispModels.Contains(DisplayModel.Result))
                            {
                                DispModels.Add(DisplayModel.Result);
                            }
                            DispLayer = (DisplayLayers)DisplayModel.Result;

                            sw.Stop();
                            RunTime = sw.ElapsedMilliseconds;
                            ToolState = E_ToolState.RunCompleted;
                            break;

                        }
                    case E_GeometryType.LineCircle:
                        {
                            row1 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线起始点.Y);
                            column1 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线起始点.X);
                            row2 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线终点.Y);
                            column2 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线终点.X);
                            centerPointrow = new HTuple(typesList.Where(u => u.名称 == "Circle1").ToList()[0].点or圆心坐标.Y);
                            centerPointcolumn = new HTuple(typesList.Where(u => u.名称 == "Circle1").ToList()[0].点or圆心坐标.X);
                            R = new HTuple(typesList.Where(u => u.名称 == "Circle1").ToList()[0].圆半径);
                            HOperatorSet.DistancePl(centerPointrow, centerPointcolumn, row1, column1, row2, column2, out Distance);
                            if (Distance.D > R.D)
                            {
                                Distancemin = new HTuple(Distance.D - R.D);
                            }
                            else
                            {
                                Distancemin = new HTuple(0);
                            }
                            
                           
                            Results.Add(new HLCResult()
                            {
                                Name = "线圆工具",

                                IsFailed = true,
                                圆心到直线距离 = Math.Round(Distance.D, 3),
                                直线到圆最短距离 = Math.Round(Distancemin.D, 3),

                            });
                            if (!DispModels.Contains(DisplayModel.Result))
                            {
                                DispModels.Add(DisplayModel.Result);
                            }
                            DispLayer = (DisplayLayers)DisplayModel.Result;

                            sw.Stop();
                            RunTime = sw.ElapsedMilliseconds;
                            ToolState = E_ToolState.RunCompleted;
                            break;

                        }
                    case E_GeometryType.PointCircle:
                        {
                            row1 = new HTuple(typesList.Where(u => u.名称 == "Point1").ToList()[0].点or圆心坐标.Y);
                            column1 = new HTuple(typesList.Where(u => u.名称 == "Point1").ToList()[0].点or圆心坐标.X);
                            centerPointrow = new HTuple(typesList.Where(u => u.名称 == "Circle1").ToList()[0].点or圆心坐标.Y);
                            centerPointcolumn = new HTuple(typesList.Where(u => u.名称 == "Circle1").ToList()[0].点or圆心坐标.X);
                            R = new HTuple(typesList.Where(u => u.名称 == "Circle1").ToList()[0].圆半径);
                            HOperatorSet.DistancePp(centerPointrow, centerPointcolumn, row1, column1, out Distance);
                            if (Distance.D > R.D)
                            {
                                Distancemin = new HTuple(Distance.D - R.D);
                            }
                            else
                            {
                                Distancemin = new HTuple(R.D- Distance.D);
                            }
                            
                           
                            Results.Add(new HPCResult()
                            {
                                Name = "点圆工具",

                                IsFailed = true,
                                圆心到点距离 = Math.Round(Distance.D, 3),
                                点到圆最短距离 = Math.Round(Distancemin.D, 3),

                            });
                            if (!DispModels.Contains(DisplayModel.Result))
                            {
                                DispModels.Add(DisplayModel.Result);
                            }
                            DispLayer = (DisplayLayers)DisplayModel.Result;

                            sw.Stop();
                            RunTime = sw.ElapsedMilliseconds;
                            ToolState = E_ToolState.RunCompleted;
                            break;

                        }
                    case E_GeometryType.PointLine:
                        {
                            row1 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线起始点.Y);
                            column1 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线起始点.X);
                            row2 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线终点.Y);
                            column2 = new HTuple(typesList.Where(u => u.名称 == "Line1").ToList()[0].直线终点.X);
                            centerPointrow = new HTuple(typesList.Where(u => u.名称 == "Point1").ToList()[0].点or圆心坐标.Y);
                            centerPointcolumn = new HTuple(typesList.Where(u => u.名称 == "Point1").ToList()[0].点or圆心坐标.X);
                            HOperatorSet.DistancePl(centerPointrow, centerPointcolumn, row1, column1, row2, column2, out Distance);
                          
                            HOperatorSet.AngleLx(row1, column1, row2, column2, out Angle);
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_k = Angle.TupleTan();
                                hv_b = centerPointcolumn - (hv_k * centerPointrow);
                            }
                            hv_xTest?.Dispose();
                            hv_xTest = 300;
                            hv_yTest?.Dispose();
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_yTest = (hv_k * hv_xTest) + hv_b;
                            }
                            Angle?.Dispose();
                            HOperatorSet.AngleLl(centerPointrow, centerPointcolumn, hv_xTest, hv_yTest, row1, column1, row2, column2, out Angle);
                            HOperatorSet.IntersectionLl(row1, column1, row2, column2, centerPointrow, centerPointcolumn, hv_xTest, hv_yTest, out Irow, out Icolumn, out IsParallel);
                            Results.Add(new HPLResult()
                            {
                                Name = "点线工具",

                                IsFailed = true,
                                垂直距离 = Math.Round(Distance.D, 3),
                                垂点坐标 = new System.Windows.Point(Math.Round(Irow.D, 3), Math.Round(Icolumn.D, 3)),

                            });
                            if (!DispModels.Contains(DisplayModel.Result))
                            {
                                DispModels.Add(DisplayModel.Result);
                            }
                            DispLayer = (DisplayLayers)DisplayModel.Result;

                            sw.Stop();
                            RunTime = sw.ElapsedMilliseconds;
                            ToolState = E_ToolState.RunCompleted;
                            break;

                        }
                    case E_GeometryType.PointPoint:
                        {
                            row1 = new HTuple(typesList.Where(u => u.名称 == "Point1").ToList()[0].点or圆心坐标.Y);
                            column1 = new HTuple(typesList.Where(u => u.名称 == "Point1").ToList()[0].点or圆心坐标.X);
                            centerPointrow = new HTuple(typesList.Where(u => u.名称 == "Circle1").ToList()[0].点or圆心坐标.Y);
                            centerPointcolumn = new HTuple(typesList.Where(u => u.名称 == "Circle1").ToList()[0].点or圆心坐标.X);
                            HOperatorSet.DistancePp(centerPointrow, centerPointcolumn, row1, column1, out Distance);
                            


                            Results.Add(new HPPResult()
                            {
                                Name = "点点工具",

                                IsFailed = true,
                                点到点距离 = Math.Round(Distance.D, 3)

                            });
                            if (!DispModels.Contains(DisplayModel.Result))
                            {
                                DispModels.Add(DisplayModel.Result);
                            }
                            DispLayer = (DisplayLayers)DisplayModel.Result;

                            sw.Stop();
                            RunTime = sw.ElapsedMilliseconds;
                            ToolState = E_ToolState.RunCompleted;
                            break;

                        }
                        default:
                        { break; }

                }
               
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
                row1?.Dispose(); row2?.Dispose();
                column1?.Dispose(); column2?.Dispose();
                row3?.Dispose(); row4?.Dispose();
                column3?.Dispose(); column4?.Dispose();
                centerPointrow?.Dispose(); centerPointcolumn?.Dispose(); R?.Dispose(); Distance?.Dispose();
                Irow?.Dispose(); Icolumn?.Dispose();
                IsParallel?.Dispose(); Angle?.Dispose(); Degree?.Dispose(); Distancemin?.Dispose(); Distancemax?.Dispose();
                hv_k?.Dispose(); hv_b?.Dispose(); hv_xTest?.Dispose(); hv_yTest?.Dispose();
                hv_Intersection?.Dispose(); Line1?.Dispose(); Line2?.Dispose(); ;
                row1 = null; row2 = null;
                column1 = null; column2 = null;
                row3 = null; row4 = null;
                column3 = null; column4 = null;
                centerPointrow = null; centerPointcolumn = null; R = null; Distance = null;
                Irow = null; Icolumn = null;
                IsParallel = null; Angle = null; Degree = null; Distancemin = null; Distancemax = null;
                hv_k = null; hv_b = null; hv_xTest = null; hv_yTest = null;
                hv_Intersection = null; Line1 = null; Line2 = null;
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
