// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：PageItem、NavPageConfig。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、System.Collections.Generic、System.IO、System.Linq、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Extension.Core.Conversion;

namespace Device.Workbench.Infrastructure
{
    /// <summary>导航页配置（视图名须与 Prism 导航注册名一致）。</summary>
    /// <param name="Title">侧栏显示标题。</param>
    /// <param name="Badge">侧栏徽标文字。</param>
    /// <param name="ViewName">Prism 视图注册名。</param>
    public sealed record PageItem(
        string Title = "",
        string Badge = "",
        string ViewName = "");

    /// <summary>
    /// 工作台导航页配置：独立文件 &lt;程序目录&gt;/Config/Pages.json，
    /// 与相机档案页「一页一文件」的落盘约定保持一致。
    /// 缺文件、坏值一律回退 <see cref="DefaultPages"/>，不抛。
    /// </summary>
    public static class NavPageConfig
    {
        /// <summary>导航页配置文件完整路径。</summary>
        public static string FileFullPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Pages.json");

        /// <summary>无配置时的默认页（缺配置、坏配置、空页都回退到这里）。</summary>
        public static IReadOnlyList<PageItem> DefaultPages { get; } =
        [
            new("相机测试", "CAM", "CameraView"),
            new("采集卡测试", "FG", "FrameGrabberView"),
            new("光源测试", "LIGHT", "LightView"),
            new("扫码器测试", "SCAN", "ScannerView"),
            new("打印机测试", "PRINT", "PrinterView"),
            new("RFID 测试", "RFID", "RfidView"),
            new("标签打印测试", "LBL", "LabelPrintView"),
        ];

        /// <summary>读配置页；缺文件/坏文件返回 null，由调用方回退 <see cref="DefaultPages"/>，不抛。</summary>
        public static List<PageItem>? Read() => JsonEx.TryRead<List<PageItem>>(FileFullPath);

        /// <summary>写配置页（目录不存在自动建）。</summary>
        public static void Write(IEnumerable<PageItem> pages) =>
            JsonEx.Save(pages.ToList(), FileFullPath);
    }
}
