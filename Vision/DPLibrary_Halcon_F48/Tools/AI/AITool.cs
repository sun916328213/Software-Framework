using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Base.DispMessage;
using DPLibrary_Halcon_F48.Tools.AI.Definition;
using DPLibrary_Halcon_F48.Tools.AI.Invi;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace DPLibrary_Halcon_F48.Tools.AI
{
    public class AITool : HVisionTool
    {
        public override E_ToolType ToolType => E_ToolType.HTool_AI;


        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"AITool_{ToolMge.Instance.GetToolCount<AITool>() + 1}";


        AIBrand aIBrand= AIBrand.Invi;
        [ToolPropertyAttribute(Group = "1.基础", DisplayName = "AI类型", Description = "AI类型",
         EditorType = E_PropertyEditorType.SingleSelect)]
        public AIBrand AIBrand
        {
            get => aIBrand;
            set
            {
                if (aIBrand != value)
                {
                    aIBrand = value;
                    OnPropertyChanged(nameof(AIBrand));
                }
            }
        }


        ToolType  toolType= Definition.ToolType.Segmentation;
        [ToolPropertyAttribute(Group = "1.基础", DisplayName = "工具类型", Description = "工具类型",
         EditorType = E_PropertyEditorType.SingleSelect)]
        public ToolType AIToolType
        {
            get => toolType;
            set
            {
                if (toolType != value)
                {
                    toolType = value;
                    OnPropertyChanged(nameof(AIToolType));
                }
            }
        }

        string modelPath;
        [ToolPropertyAttribute(DisplayName = "模型路径", Group = "1.基础", Description = "模型路径",
         EditorType = E_PropertyEditorType.Text)]
        public string ModelPath
        {
            get => modelPath;
            set
            {
                if (modelPath != value)
                {
                    modelPath = value;
                    OnPropertyChanged(nameof(ModelPath));
                }
            }
        }

        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "加载模型路径", Group = "1.基础", Description = "加载模型路径",
      EditorType = E_PropertyEditorType.Button)]
        public ButtonAction Cmd_ModelPath { get; protected set; } = new ButtonAction();

 

        double  longSide = 0.5;
        [ToolPropertyAttribute(Group = "2.推理参数", DisplayName = "最小长边", Description = "最小长边 mm",
                      EditorType = E_PropertyEditorType.Double, Min = 0 ,Step =0.1)]
        /// <summary>
        /// 最小长边
        /// </summary>
        public double LongSide
        {
            get=> longSide;
            set
            {
                if (longSide != value)
                {
                    longSide = value;
                    OnPropertyChanged(nameof(LongSide));
                }
            }
        }


        double shortSide = 0.5;
        [ToolPropertyAttribute(Group = "2.推理参数", DisplayName = "最小短边", Description = "最小短边 mm",
                      EditorType = E_PropertyEditorType.Double, Min = 0 ,Step =0.1)]
        /// <summary>
        /// 最小短边
        /// </summary>
        public double ShortSide
        {
            get => shortSide;
            set
            {
                if (shortSide != value)
                {
                    shortSide = value;
                    OnPropertyChanged(nameof(ShortSide));
                }
            }
        }

        double minArea = 0.5;
        [ToolPropertyAttribute(Group = "2.推理参数", DisplayName = "最小面积", Description = "最小面积 mm²",
                      EditorType = E_PropertyEditorType.Double, Min = 0, Step = 0.1)]
        /// <summary>
        /// 最小面积
        /// </summary>
        public double MinArea
        {
            get => minArea;
            set
            {
                if (minArea != value)
                {
                    minArea = value;
                    OnPropertyChanged(nameof(MinArea));
                }
            }
        }

        double minScore = 0.5;
        [ToolPropertyAttribute(Group = "2.推理参数", DisplayName = "最小得分", Description = "最小得分",
                      EditorType = E_PropertyEditorType.Double, Min = 0, Step = 0.1)]
        /// <summary>
        /// 最小面积
        /// </summary>
        public double MinScore
        {
            get => minScore;
            set
            {
                if (minScore != value)
                {
                    minScore = value;
                    OnPropertyChanged(nameof(MinScore));
                }
            }
        }

        private IAI aIHelper;

        public AITool()
        { 
            Cmd_ModelPath.OnBtClick+=Cmd_ModelPath_OnBtClick;
        }

        private void Cmd_ModelPath_OnBtClick(object arg1, EventArgs args)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            dialog.Multiselect = false;//该值确定是否可以选择多个文件
            dialog.Title = "请选择模型";

            if (dialog.ShowDialog() == true)
            { 
                ModelPath = dialog.FileName  ;
            }
            Config();
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

            try
            {
                if (aIHelper == null)
                    Config();

                if (aIHelper == null)
                {
                    sw.Stop();
                    ToolState = E_ToolState.Failed;
                    RunMessage = "AI 助手未初始化，请检查 AI 类型与模型路径";
                    return;
                }

                if (mImage == null)
                {
                    sw.Stop();
                    ToolState = E_ToolState.Failed;
                    RunMessage = "图像为空，无法执行推理";
                    return;
                }

                aIHelper.InferParam = new InferParam { Confidence = MinScore, Long_Side = longSide, Short_Side = shortSide, MinArea = minArea };

                List<InferResultInfo> data = aIHelper.Inference(mImage.Clone());

                if (data == null)
                {
                    sw.Stop();
                    RunTime = sw.ElapsedMilliseconds;
                    ToolState = E_ToolState.RunCompleted;
                    return;
                }
                foreach (var item in data)
                {
                    HXLD_Null xldResult = new HXLD_Null(item.ContourInfo.Xld.Clone());
                    ResultXLDs.Add(xldResult);

                    HXLD_Null xldResult2 = new HXLD_Null(item.ContourInfo.Rectangle.Clone());
                    ResultXLDs.Add(xldResult2);
 
                    DispMessages.Add(new HDispMessage()
                    {
                        Anchor = DispAnchor.Center,
                        UL = new System.Windows.Point(Math.Round(item.ContourInfo.RectInfo.CenterX, 3), Math.Round(item.ContourInfo.RectInfo.CenterY, 3)),
                        Message = $"{item.DefectInfo.DefectName}"
                    });

                    Results.Add(item);
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
        }
 

        public override void Reset()
        {
            throw new NotImplementedException();
        }

        public override void Pause()
        {
            throw new NotImplementedException();
        }

        public override void Config()
        {
            if (AIBrand == AIBrand.Aiditron)
            {

            }
            else if (AIBrand == AIBrand.Invi)
            {

                InviHelper invIHelpr = new InviHelper();
                invIHelpr.InitModel(  modelPath , (int)toolType);
                aIHelper = invIHelpr;
            }

            
        }
    }
}
