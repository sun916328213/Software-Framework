// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：IMatrixSource、MatrixData、ITool、Tool、IVisionTool、E_ToolType、E_ToolState。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、Vision.Core.Serialization、System.Collections.ObjectModel、System.ComponentModel、System.IO、System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using Vision.Core.Serialization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using Vision.Core.Presentation;

namespace Vision.Core.Tools
{
    /// <summary>
    /// IMatrixSource 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public interface IMatrixSource
    {
        string Name { get; }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetMatrix。
        /// </summary>
        string GetMatrix();
    }

    /// <summary>
    /// MatrixData 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public sealed class MatrixData
    {
        public Guid ToolID { get; set; } = Guid.Empty;

   		public string ToolName { get; set; } = "Base";
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetMatrix。
        /// 正文中的关键协作调用包括 GetMatrix；具体条件和先后次序见方法体。
        /// </summary>
        public string GetMatrix() 
        {
            IMatrixSource matrixSource = ToolMge.Instance[ToolID] as IMatrixSource;
            if (matrixSource != null)
            {
                return matrixSource.GetMatrix();
            }
            else
            {
                return "1,0,0|0,1,0|0,0,1"; //默认矩阵
            }
        }           

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </summary>
        public override string ToString()
        {
            return ToolMge.Instance[ToolID]?.Name ?? "Base";
        }
    }

    /// <summary>
    /// ITool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：INotifyPropertyChanged。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public interface ITool : INotifyPropertyChanged
    {
        event Action<ITool> RequestShowView;

        /// <summary>
        /// 名称
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// 唯一编号
        /// </summary>
        Guid ID { get; }
        /// <summary>
        /// 工具类型
        /// </summary>
        E_ToolType  ToolType { get; }
        /// <summary>
        /// 工具状态
        /// </summary>
        E_ToolState ToolState { get; }
        /// <summary>
        /// 超时时间（毫秒）负数为不限制
        /// </summary>
        int TimeOut { get; set; }
        /// <summary>
        /// 上一次运行时间
        /// </summary>
        long RunTime { get; }
        /// <summary>
        /// 文件保存地址
        /// </summary>
        string FilePath { get; set; }
        string LastFilePath { get; }
        /// <summary>
        /// 用于保存运行记录的地址
        /// </summary>
        string AutoLogFilePath { get; set; }
        /// <summary>
        /// 运行信息（故障代码）
        /// </summary>
        string RunMessage { get; set; }
        /// <summary>
        /// 用于存放工具运行后的结果
        /// </summary>
        BulkObservableCollection<IToolResult> Results { get; set; }

        ToolInputPorts Inputs { get; }
        ToolOutputPorts Outputs { get; }

        /// <summary>
        /// 运行当前处理流程；对应入口为 Run。
        /// </summary>
        void Run();
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Reset。
        /// </summary>
        void Reset();
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Pause。
        /// </summary>
        void Pause();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Config。
        /// </summary>
        void Config(   );
        /// <summary>
        /// 执行保存操作；对应入口为 Save。
        /// </summary>
        void Save();
        /// <summary>
        /// 执行保存操作；对应入口为 SaveSnapShot。
        /// </summary>
        void SaveSnapShot();
    }

    /// <summary>
    /// Tool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：ITool、IJsonOnDeserializing、IJsonOnDeserialized。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class Tool : ITool, IJsonOnDeserializing, IJsonOnDeserialized
    {
        /// <summary>
        /// 初始化 Tool 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public Tool() 
        {
            Cmd_BindingPort.OnBtClick += OnBindingPortBT_Clicked;
        }

        private IToolPresentation _presentation;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AttachPresentation。
        /// 正文中的关键协作调用包括 UseSynchronization；具体条件和先后次序见方法体。
        /// </summary>
        public void AttachPresentation(IToolPresentation presentation)
        {
            _presentation = presentation;
            Results.UseSynchronization(presentation);
        }

        public event Action<ITool> RequestShowView;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RaiseRequestShowView。
        /// </summary>
        protected void RaiseRequestShowView(ITool tool)
        {
            RequestShowView?.Invoke(tool);
        }

        
        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnBindingPortBT_Clicked。
        /// </summary>
        protected virtual void OnBindingPortBT_Clicked(object sender, EventArgs e) 
        {
            _presentation?.ShowMessage($"{Name} 端口hhhhh。。。。。。。。");
        }

        [JsonIgnore]
        protected Guid mID = Guid.NewGuid();
        [JsonIgnore]
        protected bool isFileChanged = true;
        [JsonIgnore]
        protected string mFilePath = ToolMge.Instance.FilePath;
        [JsonIgnore]
        protected E_ToolState mToolState = E_ToolState.Idle;
        [JsonIgnore]
        protected int mTimeOut = -1;
        [JsonIgnore]
        protected long mRunTime = 0;

        protected string mRunMessage = string.Empty;

        [ToolPropertyAttribute(DisplayName = "唯一ID", Group = "1.基础", Description = "用于标识当前设备的唯一ID", Order = 99)]
        public Guid ID { get { return mID; } set { mID = value; } }

        [ToolPropertyAttribute(DisplayName = "工具类型", Group = "1.基础", Description = "用于标识当前设备的唯一ID", Order = 99)]
        public virtual E_ToolType ToolType { get { return E_ToolType.OtherTool; } }

        public ToolInputPorts Inputs { get; protected set; }
        public ToolOutputPorts Outputs { get; protected set; }

        [JsonIgnore]
        public E_ToolState ToolState
        {
            get { return mToolState; }
            protected set
            {
                if (mToolState != value)
                {
                    mToolState = value;
                    OnPropertyChanged(nameof(ToolState));
                }
            }
        }

        [ToolPropertyAttribute(DisplayName = "超时时间", Group = "1.基础", Description = "用于标识当前工具的超过当前时间未完成则报警,-1为永不超时报警",
                               EditorType = E_PropertyEditorType.Int, Max = 1000000, Min = -1, Step = 10, Order = 10)]
        public int TimeOut { get { return mTimeOut; } set { mTimeOut = value; } }

        [JsonIgnore]
        public long RunTime
        {
            get { return mRunTime; }
            protected set { mRunTime = value; OnPropertyChanged(nameof(RunTime)); }
        }

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public virtual string Name { get; set; } = $"Tool_{DateTime.Now.ToString("yyyyMMdd_ HHmmssfff")}";

        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "端口配置", Group = "1.基础", Description = "用于配置绑定输入输出端口",
            EditorType = E_PropertyEditorType.Button)]
        public ButtonAction Cmd_BindingPort { get; protected set; } = new ButtonAction();


        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "保存位置", Group = "1.基础", Description = "用于保存的位置与文件", Order = 99)]
        /// <summary>
        /// 保存位置
        /// </summary>
        public string FilePath
        {
            get { return mFilePath; }
            set
            {
                if (mFilePath != value)
                {
                    mFilePath = value;
                    isFileChanged = true;
                }
            }
        }

        [JsonIgnore]
        [ToolPropertyAttribute(DisplayName = "log保存位置", Group = "1.基础", Description = "用于保存log的位置与文件", Order = 99)]
        /// <summary>
        /// 运行数据保存位置
        /// </summary>
        public string AutoLogFilePath
        {
            get;
            set;
        } = Path.Combine(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory), "Tool_SnapShotData");

        [JsonIgnore]
        [Browsable(false)]
        /// <summary>
        /// 用于存放识别的条码结果
        /// </summary>
        public BulkObservableCollection<IToolResult> Results { get; set; } = new BulkObservableCollection<IToolResult>();


        [JsonIgnore]
        public string RunMessage { get { return mRunMessage; } set { mRunMessage = value; OnPropertyChanged(nameof(RunMessage)); } }

        protected string mLastFilePath = string.Empty;
        [JsonIgnore]
        public string LastFilePath { get { return mLastFilePath; } }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnPropertyChanged。
        /// </summary>
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>
        /// 运行当前处理流程；对应入口为 Run。
        /// </summary>
        public abstract void Run();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Reset。
        /// </summary>
        public abstract void Reset();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Pause。
        /// </summary>
        public abstract void Pause();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Config。
        /// </summary>
        public abstract void Config();

        /// <summary>
        /// 执行保存操作；对应入口为 SaveSnapShot。
        /// </summary>
        public abstract void SaveSnapShot();

        /// <summary>
        /// 执行保存操作；对应入口为 Save。
        /// 正文中的关键协作调用包括 Combine、ToString、SerializeToFile、Delete；具体条件和先后次序见方法体。
        /// </summary>
        public virtual void Save()
        {
            var path = Path.Combine(FilePath, ToolType.ToString(), this.Name);
            JsonHelper.SerializeToFile(this, path, this.Name);
            if (LastFilePath != string.Empty && LastFilePath != path)//保存地址变更，需要删除原有地址数据
            {
                try
                {
                    Directory.Delete(LastFilePath, true);
                }
                finally
                {
                    mLastFilePath = path;
                }
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DoAfterDeserialized。
        /// 正文中的关键协作调用包括 Combine、ToString；具体条件和先后次序见方法体。
        /// </summary>
        protected virtual void DoAfterDeserialized()
        {
            var path = Path.Combine(FilePath, ToolType.ToString(), this.Name);
            mLastFilePath = path;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DoOnDeserializing。
        /// </summary>
        protected virtual void DoOnDeserializing()
        {

        }

        // STJ 原生回调：反序列化属性填充前触发，等价旧 Newtonsoft 的 [OnDeserializing]
        void IJsonOnDeserializing.OnDeserializing() => DoOnDeserializing();

        // STJ 原生回调：反序列化属性填充完成后触发，等价旧 Newtonsoft 的 [OnDeserialized]
        void IJsonOnDeserialized.OnDeserialized() => DoAfterDeserialized();
    }

    /// <summary>
    /// IVisionTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：Tool。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class IVisionTool : Tool
    {
        public static ObservableCollection<MatrixData> AllMatrixs { get; set; } = new ObservableCollection<MatrixData>() 
        {
            new MatrixData(){ 
            ToolID = Guid.Empty
            }
        };
    }

    /// <summary>
    /// E_ToolType 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    /// <summary>
    /// 工具类型
    /// </summary>
    public enum E_ToolType
    {
        MotionTool,
        HVisionTool,
        OtherTool,

        //MotionTool从10000开始

        //HVisionTool从20000开始
        HTool_BarCode = 20000,
        HTool_2DCode,

        HTool_TemplateMatch,
        HTool_TemplateControl_SHP,
        HTool_TemplateControl_NCC,

        HTool_NPoints,
        HTool_TransforPoint,

        HTool_FindGroup,

        HTool_Fixtrue,

        HTool_AI,

        HTool_Blob,

        /// <summary>
        /// 几何工具
        /// </summary>
        HTool_Geometry
    }

    /// <summary>
    /// 工具运行状态
    /// </summary>
    public enum E_ToolState 
    { 
        /// <summary>
        /// 空闲
        /// </summary>
        Idle,
        /// <summary>
        /// 运行中
        /// </summary>
        Running,
        /// <summary>
        /// 暂停
        /// </summary>
        Paused,
        /// <summary>
        /// 运行完成
        /// </summary>
        RunCompleted,
        /// <summary>
        /// 运行异常，流程停止
        /// </summary>
        Failed,
        /// <summary>
        /// 警告，可继续运行
        /// </summary>
        Warnning,
        /// <summary>
        /// 运行超时
        /// </summary>
        TimeOut
    }

}
