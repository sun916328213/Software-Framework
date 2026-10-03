// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：LegacyDeviceConfigImportResult、LegacyDeviceConfigImporter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls。导入不代表所有类型都会在运行时加载。
using WPFControls;
namespace WPFControls.Integration.Device;

/// <summary>Compatibility result for hosts using the original WPFControls entry point.</summary>
/// <remarks>
/// LegacyDeviceConfigImportResult 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// </remarks>
public sealed class LegacyDeviceConfigImportResult
{
    private readonly global::Device.Controls.Configuration.LegacyDeviceConfigImportResult _result;
    /// <summary>
    /// 初始化 LegacyDeviceConfigImportResult 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public LegacyDeviceConfigImportResult() : this(new()) { }
    /// <summary>
    /// 初始化 LegacyDeviceConfigImportResult 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    internal LegacyDeviceConfigImportResult(global::Device.Controls.Configuration.LegacyDeviceConfigImportResult result) => _result = result;
    public List<string> Imported => _result.Imported;
    public List<string> Skipped => _result.Skipped;
    public List<string> Warnings => _result.Warnings;
    public bool HasWarnings => _result.HasWarnings;
}

/// <summary>Legacy host entry point; migration logic is owned by Device.Controls.</summary>
/// <remarks>
/// LegacyDeviceConfigImporter 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// </remarks>
public static class LegacyDeviceConfigImporter
{
    public static string DefaultConfigDirectory => global::Device.Controls.Configuration.LegacyDeviceConfigImporter.DefaultConfigDirectory;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Import。
    /// 正文中的关键协作调用包括 Import；具体条件和先后次序见方法体。
    /// </summary>
    public static LegacyDeviceConfigImportResult Import() => new(global::Device.Controls.Configuration.LegacyDeviceConfigImporter.Import());
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Import。
    /// 正文中的关键协作调用包括 Import；具体条件和先后次序见方法体。
    /// </summary>
    public static LegacyDeviceConfigImportResult Import(string configDirectory) => new(global::Device.Controls.Configuration.LegacyDeviceConfigImporter.Import(configDirectory));
}
