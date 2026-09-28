using Common.StreamEX.Json;
using DPLibrary_Halcon_F48.Tools;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows;

namespace Vision.Core.Tools
{
    public interface IMatrixSource
    {
        string Name { get; }

        string GetMatrix();
    }

    public sealed class MatrixData
    {
        public Guid ToolID { get; set; } = Guid.Empty;

   		public string ToolName { get; set; } = "Base";
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

        public override string ToString()
        {
            return ToolMge.Instance[ToolID]?.Name ?? "Base";
        }
    }

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

        void Run();
        void Reset();
        void Pause();

        void Config(   );
        void Save();
        void SaveSnapShot();
    }

    public abstract class Tool : ITool, IJsonOnDeserializing, IJsonOnDeserialized
    {
        public Tool() 
        {
            Cmd_BindingPort.OnBtClick += OnBindingPortBT_Clicked;
        }

        public event Action<ITool> RequestShowView;

        protected void RaiseRequestShowView(ITool tool)
        {
            RequestShowView?.Invoke(tool);
        }

        
        protected virtual void OnBindingPortBT_Clicked(object sender, EventArgs e) 
        {
            MessageBox.Show($"{Name} 端口hhhhh。。。。。。。。");
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

        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public abstract void Run();

        public abstract void Reset();

        public abstract void Pause();

        public abstract void Config();

        public abstract void SaveSnapShot();

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

        protected virtual void DoAfterDeserialized()
        {
            var path = Path.Combine(FilePath, ToolType.ToString(), this.Name);
            mLastFilePath = path;
        }

        protected virtual void DoOnDeserializing()
        {

        }

        // STJ 原生回调：反序列化属性填充前触发，等价旧 Newtonsoft 的 [OnDeserializing]
        void IJsonOnDeserializing.OnDeserializing() => DoOnDeserializing();

        // STJ 原生回调：反序列化属性填充完成后触发，等价旧 Newtonsoft 的 [OnDeserialized]
        void IJsonOnDeserialized.OnDeserialized() => DoAfterDeserialized();
    }

    public abstract class IVisionTool : Tool
    {
        public static ObservableCollection<MatrixData> AllMatrixs { get; set; } = new ObservableCollection<MatrixData>() 
        {
            new MatrixData(){ 
            ToolID = Guid.Empty
            }
        };
    }

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
