using Prism.Ioc;
using Secs.Variable;
using Secs.Order.S7;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Secs.Manager
{
    /// <summary>
    /// 设备信息管理
    /// </summary>
    public class DeviceManager : ManagerBase
    {
        public DeviceManager(IContainerProvider container) : base(container)
        {
            ProcessProgramDirectory = new ConcurrentDictionary<string, ProcessProgramInfo>();
            ProcessProgramDataStore = new ConcurrentDictionary<string, ProcessProgramData>();
        }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string EQName { get; set; } = "AOI";

        /// <summary>
        /// 软件版本
        /// </summary>
        public string SoftwareVersion { get; set; } = "1.0.0";

        /// <summary>
        /// 过程程序目录 (EPPD) - 从 S7F20 接收
        /// Key: PPID
        /// </summary>
        public ConcurrentDictionary<string, ProcessProgramInfo> ProcessProgramDirectory { get; private set; }

        /// <summary>
        /// 过程程序详细数据 - 从 S7F26 接收
        /// Key: PPID
        /// </summary>
        public ConcurrentDictionary<string, ProcessProgramData> ProcessProgramDataStore { get; private set; }

        /// <summary>
        /// 过程程序发送结果事件
        /// </summary>
        public event Action<bool, string> ProcessProgramSendResult;

        public void Load(Dictionary<string, VIDInfo> pVIDs)
        {
            if (pVIDs == null) return;

            // 缺键保护：VID 配置缺失时保留默认值并告警，不再直接抛 KeyNotFoundException
            if (pVIDs.TryGetValue("DeviceName", out var nameVid) && nameVid?.Value != null)
            {
                EQName = nameVid.Value.ToString();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("警告: VID配置缺少 DeviceName，使用默认设备名 " + EQName);
            }

            if (pVIDs.TryGetValue("DeviceSoftWare", out var verVid) && verVid?.Value != null)
            {
                SoftwareVersion = verVid.Value.ToString();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("警告: VID配置缺少 DeviceSoftWare，使用默认版本号 " + SoftwareVersion);
            }
        }

        /// <summary>
        /// 更新过程程序目录 (来自 S7F20)
        /// </summary>
        public void UpdateProcessProgramDirectory(List<ProcessProgramInfo> ppList)
        {
            if (ppList == null) return;

            ProcessProgramDirectory.Clear();
            foreach (var pp in ppList)
            {
                if (!string.IsNullOrEmpty(pp.PPID))
                {
                    ProcessProgramDirectory[pp.PPID] = pp;
                }
            }

            System.Diagnostics.Debug.WriteLine($"设备过程程序目录已更新，共 {ProcessProgramDirectory.Count} 个程序");
        }

        /// <summary>
        /// 更新过程程序详细数据 (来自 S7F26)
        /// </summary>
        public void UpdateProcessProgramData(ProcessProgramData programData)
        {
            if (programData == null || string.IsNullOrEmpty(programData.PPID)) return;

            ProcessProgramDataStore[programData.PPID] = programData;

            System.Diagnostics.Debug.WriteLine($"过程程序数据已更新: PPID={programData.PPID}, Body项数={programData.PPBODY?.Count ?? 0}");
        }

        /// <summary>
        /// 获取过程程序目录列表
        /// </summary>
        public List<ProcessProgramInfo> GetProcessProgramDirectory()
        {
            return new List<ProcessProgramInfo>(ProcessProgramDirectory.Values);
        }

        /// <summary>
        /// 获取指定的过程程序信息
        /// </summary>
        public ProcessProgramInfo GetProcessProgramInfo(string ppid)
        {
            if (string.IsNullOrEmpty(ppid)) return null;
            ProcessProgramDirectory.TryGetValue(ppid, out var info);
            return info;
        }

        /// <summary>
        /// 获取指定的过程程序详细数据
        /// </summary>
        public ProcessProgramData GetProcessProgramData(string ppid)
        {
            if (string.IsNullOrEmpty(ppid)) return null;
            ProcessProgramDataStore.TryGetValue(ppid, out var data);
            return data;
        }

        /// <summary>
        /// 过程程序发送结果回调 (来自 S7F24)
        /// </summary>
        internal void OnProcessProgramSendResult(bool success, string message)
        {
            ProcessProgramSendResult?.Invoke(success, message);
        }

        /// <summary>
        /// 检查过程程序是否存在于目录中
        /// </summary>
        public bool HasProcessProgram(string ppid)
        {
            if (string.IsNullOrEmpty(ppid)) return false;
            return ProcessProgramDirectory.ContainsKey(ppid);
        }

        /// <summary>
        /// 清除过程程序目录
        /// </summary>
        public void ClearProcessProgramDirectory()
        {
            ProcessProgramDirectory.Clear();
            ProcessProgramDataStore.Clear();
        }
    }
}