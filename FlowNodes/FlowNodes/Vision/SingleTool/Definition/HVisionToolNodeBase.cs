using FlowEditor.Nodes;
using Common.StreamEX.Json;
using Ctr_WPF_F48.Ctr_Halcon.View;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using HalconDotNet;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Vision.SingleTool.Definition
{
    /// <summary>
    /// 视觉工具节点基类：封装视觉工具的注册/还原、生命周期、编辑器窗口、编辑按钮等重复逻辑。
    /// 子类只需提供 [FlowTool] 特性、输入输出属性与 ExecuteAsync 即可。
    /// </summary>
    /// <typeparam name="T">视觉工具类型（如 HTool_Blob、HTool_TemplateMatch、HTool_ImageFile），须有无参构造。</typeparam>
    public abstract class HVisionToolNodeBase<T> : NodeModel, IVisionToolNode where T : HVisionTool, new()
    {
        private T _visionTool = new T();

        /// <summary>渲染/执行用工具。加载流程图中若关联了已保存的工具，则会指向 ToolMge 里那份带数据的实例。不随 .flow 持久化（工具含 Halcon 句柄，无法 JSON 序列化）。</summary>
        [JsonIgnore]
        public T visionTool => _visionTool;

        /// <summary>关联键：持久化到 .flow，加载时据此从 ToolMge 找回已保存的工具（Name 是跨重启的稳定键）。</summary>
        public string VisionToolName { get; set; } = "";

        // ── 工具引用计数：跨所有流程统计每个工具名的挂载节点数，用于孤儿工具清理 ──
        private static readonly object s_RefLock = new();
        private static readonly Dictionary<string, int> s_RefCount = new();

        private readonly Ctr_HVisionTool_DP _editor = new Ctr_HVisionTool_DP();

        [NodeProperty(DisplayName = "输入图像", Group = "1.基础信息", Order = 2, Description = "输入图像",IsInput =true)]
        [JsonIgnore]
        public  virtual HObject Image { get; set; }

        private int _resultCount;
        /// <summary>最近一次执行产生的结果数量（可绑定到输出端口）。</summary>
        [JsonIgnore]
        public int ResultCount
        {
            get => _resultCount;
            set => SetProperty(ref _resultCount, value);
        }

        /// <summary>最近一次执行产生的 IToolResult 结果列表（可绑定到输出端口，作为统一输出）。</summary>
        [JsonIgnore]
        public List<IToolResult> Results => visionTool.Results?.ToList() ?? new List<IToolResult>();

        /// <summary>统一执行：注册工具、赋入图像并运行，随后刷新结果计数。</summary>
        protected void RunTool()
        {
            EnsureRegistered();
            visionTool.Image = Image;
            visionTool.Run();
            ResultCount = visionTool.Results?.Count ?? 0;
        }

        [NodeAction("编辑", Order = 0, IconResource = "CodeBraces")]
        public void EditScript()
        {
            OpenVisionTool();
        }

        /// <summary>
        /// 确保当前工具已注册进 ToolMge（幂等）。若 .flow 中记录了 VisionToolName，
        /// 先从 ToolMge 找回带数据的那份工具；找不到再注册空实例兜底。覆盖 ToolBlock 内部节点这类未走 OnAttached 的路径。
        /// </summary>
        protected void EnsureRegistered()
        {
            if (RelinkSavedTool()) return;

            // 文件加载但按名找不到工具：新建实例改名对齐引用名，保证持久化/再加载一致
            if (!string.IsNullOrEmpty(VisionToolName) && _visionTool.Name != VisionToolName)
                _visionTool.Name = VisionToolName;

            if (ToolMge.Instance[_visionTool.ID] == null)
                ToolMge.Instance.Add(_visionTool);

            if (string.IsNullOrEmpty(VisionToolName))
                VisionToolName = _visionTool.Name;
        }

        /// <summary>
        /// 从 .flow 加载的节点 visionTool 初始是空实例。若记录了 VisionToolName，
        /// 则从 ToolMge 找回那份带数据（参数）的工具替换之。
        /// </summary>
        protected bool RelinkSavedTool()
        {
            if (string.IsNullOrEmpty(VisionToolName)) return false;
            var saved = ToolMge.Instance[VisionToolName] as T;
            if (saved == null)
            {
                // 内存中已被清理（如双击新建节点关闭时移除过），尝试从磁盘按名称恢复
                saved = TryLoadToolFromDisk(VisionToolName);
                if (saved == null) return false;
                ToolMge.Instance.Add(saved);
            }
            if (saved == _visionTool) return false;
            _visionTool = saved;
            return true;
        }

        /// <summary>按工具名称从磁盘加载已保存的视觉工具（Config/Tools/&lt;ToolType&gt;/&lt;Name&gt;/&lt;Name&gt;.json）。</summary>
        private T? TryLoadToolFromDisk(string name)
        {
            try
            {
                var filePath = Path.Combine(ToolMge.Instance.FilePath, _visionTool.ToolType.ToString(), name, $"{name}.json");
                if (!File.Exists(filePath)) return null;
                var tool = JsonHelper.DeserializeFromFile(filePath, typeof(T)) as T;
                if (tool != null && tool.Name != name) tool.Name = name;
                return tool;
            }
            catch
            {
                return null;
            }
        }

        public override void OnAttached(IFlowChartService service)
        {
            base.OnAttached(service);
            EnsureRegistered();
            AcquireToolRef();
        }

        public override void OnDetached()
        {
            base.OnDetached();
            ReleaseToolRef();
            // 加载/清空/撤销重做等批量操作期间不移除工具（它们会先 detach 再按名 re-attach），
            // 仅"真正删除节点"后且无任何节点再引用时清理孤儿工具，防止目录无限膨胀。
            if (Service?.IsBatchOperation != true)
                RemoveToolIfUnreferenced();
        }

        /// <summary>挂载引用计数 +1。</summary>
        private void AcquireToolRef()
        {
            if (string.IsNullOrEmpty(VisionToolName)) return;
            lock (s_RefLock)
            {
                s_RefCount.TryGetValue(VisionToolName, out var c);
                s_RefCount[VisionToolName] = c + 1;
            }
        }

        /// <summary>挂载引用计数 -1。</summary>
        private void ReleaseToolRef()
        {
            if (string.IsNullOrEmpty(VisionToolName)) return;
            lock (s_RefLock)
            {
                if (!s_RefCount.TryGetValue(VisionToolName, out var c)) return;
                if (c <= 1) s_RefCount.Remove(VisionToolName);
                else s_RefCount[VisionToolName] = c - 1;
            }
        }

        /// <summary>若当前无任何节点引用该工具，则从 ToolMge 移除（磁盘文件保留，重载时仍可按名恢复）。</summary>
        private void RemoveToolIfUnreferenced()
        {
            bool referenced;
            lock (s_RefLock)
            {
                referenced = !string.IsNullOrEmpty(VisionToolName) && s_RefCount.ContainsKey(VisionToolName);
            }
            if (!referenced)
                ToolMge.Instance.Remove(_visionTool);
        }

        /// <summary>流程图保存时同步把视觉工具持久化到磁盘（Config/Tools/<ToolType>/<Name>/<Name>.json）。</summary>
        public override void OnFlowSaved()
        {
            EnsureRegistered();
            ToolMge.Instance.Save(_visionTool);
        }

        #region
        private void OpenVisionTool()
        {
            EnsureRegistered();
            _editor.HTool = _visionTool;
            var window = new Window
            {
                Title = $"视觉工具 - {Name}",
                Content = _editor,
                Owner = Application.Current?.MainWindow,
                Width = 900,
                Height = 700,
                SizeToContent = SizeToContent.Manual,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Style = Application.Current?.TryFindResource("MaterialDesignWindow") as Style,
            };
            window.ShowDialog();
        }
        #endregion
    }

    /// <summary>视觉节点统一接口：暴露关联的工具名，用于跨节点引用计数。</summary>
    public interface IVisionToolNode
    {
        string VisionToolName { get; }
    }
}
