// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：RegionNames。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
namespace Device.Workbench.Infrastructure
{
    /// <summary>Prism 区域名。</summary>
    public static class RegionNames
    {
        public const string MainRegion = "MainRegion";
    }
}
