// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：RegionNames。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Workbench。导入不代表所有类型都会在运行时加载。
using Communication.Workbench;
namespace Communication.Workbench.Infrastructure;

/// <summary>Prism 区域名。</summary>
public static class RegionNames
{
    public const string MainRegion = "MainRegion";
}
