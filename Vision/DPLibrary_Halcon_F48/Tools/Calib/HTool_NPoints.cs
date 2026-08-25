using Common.StreamEX.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Tools.TemplateMatch;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools
{
    /// <summary>
    /// 用于标定的点
    /// </summary>
    public class CalibPoint
    {
        public double Rx { get; set; }
        public double Ry { get; set; }
        public double Cx { get; set; }
        public double Cy { get; set; }
    }

    public class HTool_NPoints : HVisionTool
    {

        public HTool_NPoints() 
        {
            CalibPoints_XY.CollectionChanged += (s, e) => 
            {
                mRPoint = new Point();
            };    
        }

        protected Point mRPoint = new Point();
        protected double mXEq = 0;
        protected double mYEq = 0;

        public override E_ToolType ToolType { get { return E_ToolType.HTool_NPoints; } }

        [ToolPropertyAttribute(Group = "2.标定参数", DisplayName = "点位列表XY", Description = "用于生成矩阵的点位列表，进行xy向标定时最少3点",
                                EditorType = E_PropertyEditorType.DataGridList)]
        public ObservableCollection<CalibPoint> CalibPoints_XY { get; set; } = new ObservableCollection<CalibPoint>();

        [ToolPropertyAttribute(Group = "2.标定参数", DisplayName = "点位列表R", Description = "用于生成矩阵的点位列表，进行R向标定时最少3点",
                                EditorType = E_PropertyEditorType.DataGridList)]
        public ObservableCollection<CalibPoint> CalibPoints_R { get; set; } = new ObservableCollection<CalibPoint>();

        [ToolPropertyAttribute(Group = "3.计算结果", DisplayName = "X当量", Description = "像素Cx与Rx的比值",
                        EditorType = E_PropertyEditorType.Double)]
        public double XEq
        {
            get => mXEq;
            set
            {
                if (mXEq != value)
                {
                    mXEq = value;
                    OnPropertyChanged(nameof(XEq));
                }
            }
        }

        [ToolPropertyAttribute(Group = "3.计算结果", DisplayName = "Y当量", Description = "像素Cy与Ry的比值",
                        EditorType = E_PropertyEditorType.Double)]
        public double YEq
        {
            get => mYEq;
            set
            {
                if (mYEq != value)
                {
                    mYEq = value;
                    OnPropertyChanged(nameof(YEq));
                }
            }
        }

        [ToolPropertyAttribute(Group = "3.计算结果", DisplayName = "旋转中心", Description = "计算的旋转中心",
                                EditorType = E_PropertyEditorType.Text)]
        public Point RPoint { get { return mRPoint; }
            set 
            {
                if (mRPoint != value)
                {
                    mRPoint = value;
                    OnPropertyChanged(nameof(RPoint));
                }
            }
        }

        string mCam2toolRoot = string.Empty;

        [ToolPropertyAttribute(Group = "3.计算结果", DisplayName = "计算矩阵", Description = "计算矩阵",
                        EditorType = E_PropertyEditorType.Text)]
        public string Cam2toolRoot
        {
            get { return mCam2toolRoot; }
            set 
            {
                if (mCam2toolRoot != value)
                {
                    mCam2toolRoot = value;
                    OnPropertyChanged(nameof(Cam2toolRoot));
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

            HTuple cx = new HTuple(), cy = new HTuple(), rx = new HTuple(), ry = new HTuple();
            HTuple cam2toolRoot = null;
            HTuple rx2 = new HTuple(), ry2 = new HTuple();
            HObject pointCont = null;
            HTuple rowCenter = new HTuple(), colCenter = new HTuple(), radius = new HTuple(), 
                   startPhi = new HTuple(), endPhi = new HTuple(), pointOrder = new HTuple();
            HTuple obj2Cam = null,toolRoot = null;
            try
            {
                if (CalibPoints_XY.Count >= 3)
                {
                    foreach (var item in CalibPoints_XY)
                    {
                        cx = cx.TupleConcat(item.Cx);
                        cy = cy.TupleConcat(item.Cy);
                        rx = rx.TupleConcat(item.Rx);
                        ry = ry.TupleConcat(item.Ry);

                        ////绘制相机点
                        HOperatorSet.GenCircle(out HObject circlexld, item.Cy, item.Cx,2);

                        HROI_Null xldResult = new HROI_Null(circlexld);

                        ResultRegions.Add(xldResult);
                    }

                    HOperatorSet.VectorToHomMat2d(cx, cy, rx, ry, out cam2toolRoot);
                    double[][] cam2toolRootArr = new double[3][] { new double[3], new double[3], new double[3] { 0, 0, 1 } };
                    for (int i = 0; i < cam2toolRoot.DArr.Length; i++)
                    {
                        int row = i / 3;
                        int col = i % 3;
                        cam2toolRootArr[row][col] = Math.Round(cam2toolRoot.DArr[i], 4);
                    }
                    Cam2toolRoot = cam2toolRootArr.ToArrString();

                    //计算比例
                    // 使用HomMat2dToAffinePar来提取仿射参数，包括缩放系数
                    HTuple sx, sy, phi, theta, tx, ty;
                    HOperatorSet.HomMat2dToAffinePar(cam2toolRoot, out sx, out sy, out phi, out theta, out tx, out ty);

                    XEq = Math.Round(sx.D, 4);
                    YEq = Math.Round(sy.D, 4);
                }


                if (CalibPoints_R.Count >= 3)
                {
                    //需要先将相机转换至机器人坐标（在存在xy转换矩阵的时候）
                    if (mCam2toolRoot != null)
                    {
                        foreach (var item in CalibPoints_R)
                        {
                            obj2Cam?.Dispose(); obj2Cam = null;
                            toolRoot?.Dispose(); toolRoot = null;
                            HOperatorSet.HomMat2dIdentity(out obj2Cam);
                            HOperatorSet.HomMat2dTranslate(obj2Cam, item.Cx, item.Cy, out obj2Cam);
                            HOperatorSet.HomMat2dCompose(cam2toolRoot, obj2Cam, out toolRoot);

                            rx2 = rx2.TupleConcat(toolRoot[2]);
                            ry2 = ry2.TupleConcat(toolRoot[5]);
                        }
                        //绘制旋转中心
                        HOperatorSet.GenContourPolygonXld(out pointCont, rx2, ry2);
                        HOperatorSet.FitCircleContourXld(pointCont, "algebraic", -1, 0, 0, 3, 2,
                            out rowCenter, out colCenter, out radius, out startPhi, out endPhi, out pointOrder);

                        RPoint = new Point(colCenter.D, rowCenter.D);
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
                cx?.Dispose(); cx = null;
                cy?.Dispose(); cy = null;
                rx?.Dispose(); rx = null;
                ry?.Dispose(); ry = null;

                cam2toolRoot?.Dispose(); cam2toolRoot = null;

                rx2?.Dispose(); rx2 = null;
                ry2?.Dispose(); ry2 = null;
                pointCont?.Dispose(); pointCont = null;
                obj2Cam?.Dispose(); obj2Cam = null;
                rowCenter?.Dispose(); rowCenter = null;
                colCenter?.Dispose(); colCenter = null;
                radius?.Dispose(); radius = null;
                startPhi?.Dispose(); startPhi = null;
                pointOrder?.Dispose(); pointOrder = null;
                obj2Cam?.Dispose(); obj2Cam = null;
                toolRoot?.Dispose(); toolRoot = null;
                endPhi?.Dispose(); endPhi = null;

            }
        }

        public override void Save()
        {
            base.Save();
        }

    }
}
