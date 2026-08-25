using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Base;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Shapes;

namespace DPLibrary_Halcon_F48.Tools.FindLine
{
    public class HTool_FindLineCanny : HVisionTool
    {

        double mAlpha = 1;
        double mMinLine = 10;
        double mMinLineUnioned = 400;

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"FindLineCanny_{ToolMge.Instance.GetToolCount<HTool_FindLineCanny>() + 1}";


        [ToolPropertyAttribute(Group = "2.检测参数", DisplayName = "滤波器", Description = "大值导致强平滑，从而细节更少",
                                EditorType = E_PropertyEditorType.Double, Min = 0, Step = 0.01)]
        /// <summary>
        /// 滤波器
        /// </summary>
        public double Alpha
        {
            get
            {
                return mAlpha;
            }
            set
            {
                if (mAlpha != value)
                {
                    mAlpha = value;
                    OnPropertyChanged(nameof(Alpha));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.检测参数", DisplayName = "线段长度Min", Description = "最短线段的长度,用于第一次筛选",
                        EditorType = E_PropertyEditorType.Double, Min = 0, Step = 1)]
        /// <summary>
        /// 线段长度Min
        /// </summary>
        public double MinLine
        {
            get
            {
                return mMinLine;
            }
            set
            {
                if (mMinLine != value)
                {
                    mMinLine = value;
                    OnPropertyChanged(nameof(MinLine));
                }
            }
        }


        [ToolPropertyAttribute(Group = "2.检测参数", DisplayName = "合并后线段度Min", Description = "最短线段的长度,用于第二次筛选",
                EditorType = E_PropertyEditorType.Double, Min = 0, Step = 1,Order = 2)]
        /// <summary>
        /// 合并后线段度Min
        /// </summary>
        public double MinLineUnioned
        {
            get
            {
                return mMinLineUnioned;
            }
            set
            {
                if (mMinLineUnioned != value)
                {
                    mMinLineUnioned = value;
                    OnPropertyChanged(nameof(MinLineUnioned));
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


            HObject temp = null, roiImage = null, hRegions = null;

            HObject edges,splitEdges;
            HObject selectEdges,unionEdges;
            HObject lines = new HObject();
            HObject line = null;
            HTuple row_start, col_start, row_end, col_end,nr,nc, dist;
            HTuple row, col;

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

            HOperatorSet.EdgesSubPix(roiImage, out edges, "canny", mAlpha, 0, 255);

            HOperatorSet.SegmentContoursXld(edges, out splitEdges, "lines", 5, 4, 2);

            HOperatorSet.SelectShapeXld(splitEdges, out selectEdges, "contlength", "and", mMinLine, "max");

            // 合并共线的 XLD
            HOperatorSet.UnionCollinearContoursXld(
                selectEdges,
                out unionEdges,
                10,                      // MaxDist: 允许端点间距
                10,
                3,                     // MaxOverlap
                (10 / 180) * Math.PI,   // MaxAngleDiff: 角度差（5°）
                "attr_forget"
            );

            HOperatorSet.SelectShapeXld(unionEdges, out unionEdges, "contlength", "and", mMinLineUnioned, "max");

            HOperatorSet.FitLineContourXld(unionEdges, "tukey", -1, 0, 5, 2,out row_start, out col_start, out row_end, out col_end, out nr, out nc, out dist);

            //HOperatorSet.CountObj(unionEdges, out edgeCount);
            for (int i = 0; i < row_start.Length/*edgeCount.D*/; i++)
            {
                line?.Dispose(); line = null;
                //HOperatorSet.SelectObj(unionEdges, out line, i + 1);
                //HOperatorSet.GetContourGlobalAttribXld(line, "cont_approx", out approx);

                row = new HTuple(row_start[i].D, row_end[i].D);
                col = new HTuple(col_start[i].D, col_end[i].D);

                HOperatorSet.GenContourPolygonXld(out line, row, col);
                I_HXLD xld = new HXLD_Null(line);
                ResultXLDs.Add(xld);

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
    }
}
