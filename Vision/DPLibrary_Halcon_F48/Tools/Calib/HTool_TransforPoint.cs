using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using HalconDotNet;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools
{
    public class HTool_TransforPoint : HVisionTool
    {
 
        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "输入点位", Group = "2.转换参数", Description = "用于转化的点位数据", 
            EditorType = E_PropertyEditorType.Text)]
        public Point InputPoint { get; set; }

        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "输入旋转", Group = "2.转换参数", Description = "用于转化的旋转数据，单位°",
            EditorType = E_PropertyEditorType.Text)]
        public double Angle { get; set; } = 0;

        public Guid ModelID { get; set; }

        [JsonIgnore]
        /// <summary>
        /// 用于存储模板信息
        /// </summary>
        public HTool_NPoints mCalibModel
        {
            get
            {
                return (HTool_NPoints)ToolMge.Instance[ModelID];
            }
        }

        protected void ChangePointsModel(object sender, EventArgs e)
        {
            if (mCalibModel == null)
            {
                HTool_NPoints tool = new HTool_NPoints();
                ToolMge.Instance.Add(tool);
                ModelID = tool.ID;
            }
            RaiseRequestShowView(mCalibModel);
        }

        /// <summary>
        /// 获取所有HTool_NPoints实例
        /// </summary>
        public static List<Guid> AllHTool_NPoints
        {
            get
            {
                List<Guid> result = new List<Guid>();
                int count = ToolMge.Instance.GetToolCount();
                for (int i = 0; i < count; i++)
                {
                    if (ToolMge.Instance[i] is HTool_NPoints)
                    {
                        Guid id =  ToolMge.Instance[i].ID;
                        result.Add(id);
                    }
                }
                return result;
            }
        }
        /// <summary>
        /// HTool_TransforPoint标定工具ID
        /// </summary>
        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "标定ID", Group = "1.基础", Description = "用于创建或修改标定信息",
            EditorType = E_PropertyEditorType.SingleSelect, 
            ListItemsSourceType = typeof(HTool_TransforPoint), 
            ListItemsSourceName = nameof(AllHTool_NPoints))]
        public Guid  CalibModelID { get; set; }  = Guid.Empty;

        public override E_ToolType ToolType { get { return E_ToolType.HTool_TransforPoint; } }

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

            HTuple cx = new HTuple(), cy = new HTuple(), rx = new HTuple(), ry = new HTuple();
            HTuple rx2 = new HTuple(), ry2 = new HTuple();
            HObject pointCont = null;
            HTuple rowCenter = new HTuple(), colCenter = new HTuple(), radius = new HTuple(),
                   startPhi = new HTuple(), endPhi = new HTuple(), pointOrder = new HTuple();
            HTuple obj2Cam = null, toolRoot = null;
            HTuple cam2toolRoot = new HTuple(), cam2toolRootR  = null;

            try
            {
                if (mCalibModel == null)
                {
                    throw new Exception("未配置对应的标定文件");
                };
                double[][] doubles = mCalibModel.Cam2toolRoot.ToDoubles();

                for (global::System.Int32 i = 0; i < doubles.Length; i++)
                {
                    for (global::System.Int32 j = 0; j < doubles[i].Length; j++)
                    {
                        cam2toolRoot = cam2toolRoot.TupleConcat(doubles[i][j]);
                    }
                }

                //计算原有对应点位
                HOperatorSet.HomMat2dIdentity(out obj2Cam);
                HOperatorSet.HomMat2dTranslate(obj2Cam, InputPoint.X, InputPoint.Y, out obj2Cam);
                HOperatorSet.HomMat2dCompose(cam2toolRoot, obj2Cam, out toolRoot);

                HOperatorSet.HomMat2dRotate(toolRoot,Math.PI * (Angle / 180), mCalibModel.RPoint.X, mCalibModel.RPoint.Y, out cam2toolRootR);

                TransforPointResult result = new TransforPointResult()
                {
                    InputPoint = InputPoint,
                    OutputPoint_T = new Point(toolRoot[2], toolRoot[5]),
                    OutputPoint_TR = new Point(cam2toolRootR[2], cam2toolRootR[5])
                };
                result.Dx = Math.Round( result.OutputPoint_T.X - result.OutputPoint_TR.X,3);
                result.Dy = Math.Round( result.OutputPoint_T.Y - result.OutputPoint_TR.Y,3);

                Results.Add(result);

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
            if (mCalibModel != null)
            {
                mCalibModel.Save();
            }
        }
    }
}
