// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolMge、ManageModel、ToolModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core.Serialization、Vision.Core、Vision.Core.Tools、System.IO。导入不代表所有类型都会在运行时加载。
using Vision.Core.Serialization;
using Vision.Core;
using Vision.Core.Tools;
using System.IO;

// ⛔ 历史坑（P2 · 2026-09-28 收口）：本文件原先声明的是
//    namespace DPLibrary_Halcon_F48.Tools —— 与**真项目** DPLibrary_Halcon_F48 的
//    Tools 命名空间同名。跨程序集同名命名空间会被编译期合并成一个逻辑命名空间，
//    于是 Halcon 侧（HTool_* 等）**不写 using 就能直接用 ToolMge**，
//    而 Vision.Core 也「声明」了一个属于别的项目的命名空间。
//    P2 按《Vision_Architecture_Optimization_Plan v1.2》§5.1 把它收回 Vision.Core.Tools，
//    代价是 Halcon 侧 13 个文件必须显式 using Vision.Core.Tools（已补）。
namespace Vision.Core.Tools
{
    /// <summary>
    /// ToolMge 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：Singleton&lt;ToolMge&gt;、ILog_DP。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ToolMge : Singleton<ToolMge>,ILog_DP
    {
        /// <summary>
        /// 初始化 ToolMge 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        ToolMge()
        {
            mToolList = new List<ITool>();
        }

        public ToolBus ToolBus { get; } = new ToolBus();

        /// <summary>
        /// 用于存放扫码模组的列表
        /// </summary>
        readonly List<ITool> mToolList;
        /// <summary>
        /// 用于线程遍历List的安全锁
        /// </summary>
        readonly object mListLock = new object();

        /// <summary>
        /// 写日志
        /// </summary>
        protected void Log(string message)
        {
            var time = DateTime.Now;

            if (OnLog != null)
            {
                OnLog.Invoke(time, message);
            }
#if DEBUG
            Console.WriteLine($"[Default][{time:yyyy-MM-dd HH:mm:ss}][CodeReaderMge] {message}");
#endif
        }
        /// <summary>
        /// 写日志
        /// </summary>
        protected void LogError(string message)
        {
            var time = DateTime.Now;

            if (OnLogError != null)
            {
                OnLogError.Invoke(time, message);
            }
#if DEBUG
            Console.WriteLine($"[Error][{time:yyyy-MM-dd HH:mm:ss}][CodeReaderMge] {message}");
#endif
        }


        /// <summary>
        /// 用于记录运行信息的log
        /// </summary>
        public event Action<DateTime, string> OnLog;
        /// <summary>
        /// 用于记录异常信息的log
        /// </summary>
        public event Action<DateTime, string> OnLogError;

        /// <summary>
        /// 用于存储管理的导航数据
        /// </summary>
        string mMgeFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ToolMge");

        /// <summary>
        /// 存放实际工具配置的地址
        /// </summary>
        public string FilePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Tools");

        /// <summary>
        /// 通过名称索引获取阅读器
        /// </summary>
        /// <param name="name">阅读器名称</param>
        /// <returns>对应的阅读器实例，不存在则返回null</returns>
        public ITool this[string name]
        {
            get
            {
                lock (mListLock)
                {
                    return mToolList.Find(o => { return o.Name == name; });
                }
            }
        }

        public ITool this[int index] 
        {
            get
            {
                if (index < mToolList.Count)
                {
                    lock (mListLock)
                    {
                        return mToolList[index];
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public ITool this[Guid id]
        {
            get
            {
                lock (mListLock)
                {
                    return mToolList.Find(o => {
                        return o.ID == id;
                    });
                }
            }
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetToolCount。
        /// </summary>
        public int GetToolCount()
        {
            return mToolList.Count;
        }
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetToolCount。
        /// 正文中的关键协作调用包括 Count、OfType；具体条件和先后次序见方法体。
        /// </summary>
        public int GetToolCount<T>()
        {
            return mToolList.OfType<T>().Count();
        }

        /// <summary>
        /// 添加读码器实例
        /// </summary>
        /// <param name="reader">要添加的阅读器</param>
        /// <returns>0表示成功，-1表示已存在</returns>
        public int Add(ITool tool) 
        {
            if (tool == null)
            {
                LogError("读码器实例不能为null");
                return -1; 
            }

            lock (mListLock)
            {
                if (!mToolList.Contains(tool))
                {
                    mToolList.Add(tool);

                    if (tool is IMatrixSource)//当工具为矩阵数据时，添加到全局矩阵池
                    {
                        IVisionTool.AllMatrixs.Add(new MatrixData()
                        {
                            ToolID = tool.ID,
                            ToolName = tool.Name
                        });
                    }

                    Log($"读码器实例:{tool.Name} 添加成功");
                    return 0;
                }
                else
                {
                    LogError($"读码器实例:{tool.Name} 添加失败，重复添加");
                    return -1; // 重复添加
                }
            }
        }

        /// <summary>
        /// 移除读码器实例
        /// </summary>
        /// <param name="reader">要移除的读码器</param>
        /// <returns>0表示成功，-1表示不存在</returns>
        public int Remove(ITool tool) 
        {
            if (tool == null)
            {
                LogError("读码器实例不能为null");
                return -1;
            }
            lock (mListLock)
            {
                if (mToolList.Contains(tool))
                {
                    mToolList.Remove(tool);
                    if (tool is IMatrixSource)
                    {
                        var matrixData = IVisionTool.AllMatrixs.FirstOrDefault(x => x.ToolID == tool.ID);
                        if (matrixData != null)
                        {
                            IVisionTool.AllMatrixs.Remove(matrixData);
                        }
                    }
                    Log($"读码器实例:{tool.Name} 移除成功");
                    return 0;
                }
                else
                {
                    LogError($"读码器实例:{tool.Name} 移除失败，管理器中不存在该项");
                    return -1; // 当前不存在该项
                }
            }
        }

        /// <summary>
        /// 加载配置文件初始化阅读器列表
        /// </summary>
        /// <returns>true表示加载成功，false表示加载失败</returns>
        public bool Config() 
        {
            string configPath = Path.Combine(FilePath, "ToolMge.json");

            if (!File.Exists(configPath) || new FileInfo(configPath).Length == 0)
            {
                LogError($"Tool manager config is missing or empty: {configPath}");
                return false;
            }

            ManageModel model;
            try
            {
                model = JsonHelper.DeserializeFromFile<ManageModel>(configPath);
            }
            catch (Exception ex)
            {
                LogError($"Tool manager config load failed: {configPath}, {ex.Message}");
                return false;
            }

            if (model?.Tools == null)
                return false;

            lock (mListLock)
            {
                mToolList.Clear();
                IVisionTool.AllMatrixs.Clear();
                IVisionTool.AllMatrixs.Add(new MatrixData
                {
                    ToolID = Guid.Empty,
                    ToolName = "Base"
                });
                foreach (var item in model.Tools)
                {
                    // 加载具体工具配置
                    string readerPath;
                    if (string.IsNullOrWhiteSpace(item.FilePath))
                    {
                        readerPath = this.FilePath;
                    }
                    else
                    {
                        readerPath = item.FilePath;
                    }
                    string filePath = Path.Combine(readerPath, $"{item.ToolType}", $"{item.Name}", $"{item.Name}.json");
                    ITool tool;
                    Type t = FrameworkTypeResolver.ResolvePersistedType(item.TypeName);
                    if (t == null)
                    {
                        LogError($"Tool type not found: {item.Name}, {item.TypeName}");
                        continue;
                    }

                    if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                    {
                        LogError($"工具实例:{item.Name} 文件不存在 {filePath}");
                        tool = (ITool)Activator.CreateInstance(t);
                    }
                    else
                    {
                        try
                        {
                            tool = (ITool)JsonHelper.DeserializeFromFile(filePath, t);
                        }
                        catch (Exception ex)
                        {
                            LogError($"Tool config load failed: {item.Name}, {filePath}, {ex.Message}");
                            tool = (ITool)Activator.CreateInstance(t);
                        }
                    }
                    if (tool == null)
                    {
                        LogError($"Tool instance could not be created: {item.Name}, {item.TypeName}");
                        continue;
                    }

                    tool.FilePath = readerPath;
                    tool.Name = item.Name;
                    Add(tool);
                }
            }
            return true;
        }

        /// <summary>
        /// 执行保存操作；对应入口为 Save。
        /// 正文中的关键协作调用包括 SaveManager、Save；具体条件和先后次序见方法体。
        /// </summary>
        public void Save()
        {
            SaveManager();
            foreach (var item in mToolList)
            {
                item.Save();
            }
        }

        /// <summary>
        /// 执行保存操作；对应入口为 Save。
        /// 正文中的关键协作调用包括 SaveManager、Save；具体条件和先后次序见方法体。
        /// </summary>
        public void Save(params ITool[] tools) 
        {
            SaveManager();
            foreach (var item in tools)
            {
                item.Save();
            }
        }

        /// <summary>
        /// 保存管理器配置
        /// </summary>
        void SaveManager()
        {
            var model = new ManageModel()
            {
                FilePath = this.FilePath,
                Tools = mToolList.Select(r => new ToolModel
                {
                    Name = r.Name,
                    ToolType = r.ToolType,
                    TypeName = r.GetType().AssemblyQualifiedName,
                    FilePath = r.FilePath
                }).ToList()
            };

            var path = Path.Combine(FilePath, "ToolMge.json");

            var json = JsonHelper.Serialize(model);// JsonConvert.SerializeObject(model/*, JsonSettings*/);
            JsonHelper.AtomicWrite(path, json);
            Log($"工具集合 保存成功{path}");
        }
    }

    //用于中转保存的数据模型
    class ManageModel
    {
        public string FilePath { get; set; } 
        public List<ToolModel> Tools { get; set; }
    }

    /// <summary>
    /// ToolModel 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    class ToolModel
    {
        public string Name { get; set; } = "";
        public E_ToolType ToolType { get; set; }
        public string TypeName { get; set; } = ""; // 类型名称
        public string FilePath { get; set; } = ""; // 配置文件路径
    }


}
