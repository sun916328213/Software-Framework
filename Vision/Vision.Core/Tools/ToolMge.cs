using Common.StreamEX.Json;
using Vision.Core;
using Vision.Core.Tools;
using System.IO;

namespace DPLibrary_Halcon_F48.Tools
{
    public class ToolMge : Singleton<ToolMge>,ILog_DP
    {
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

        public int GetToolCount()
        {
            return mToolList.Count;
        }
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
                        IVisionTool.AllMatrixs.Add(new Vision.Core.Tools.MatrixData()
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
                    Type t = Type.GetType(item.TypeName);
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

        public void Save()
        {
            SaveManager();
            foreach (var item in mToolList)
            {
                item.Save();
            }
        }

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

    class ToolModel
    {
        public string Name { get; set; } = "";
        public E_ToolType ToolType { get; set; }
        public string TypeName { get; set; } = ""; // 类型名称
        public string FilePath { get; set; } = ""; // 配置文件路径
    }


}
