namespace CommunicationNew.Composition;

/// <summary>
/// 保留旧命名空间的 factory 接口，避免现有调用方立即迁移。
/// 新代码应依赖 <see cref="CommunicationNew.Core.ICommunicationDeviceFactory"/>。
/// </summary>
[Obsolete("请使用 CommunicationNew.Core.ICommunicationDeviceFactory；该兼容接口将在后续主版本移除。")]
public interface ICommunicationDeviceFactory : CommunicationNew.Core.ICommunicationDeviceFactory
{
}
