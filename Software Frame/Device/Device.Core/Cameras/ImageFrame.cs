// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：ImageFrame。成员说明可结合下方具体实现阅读。
namespace Device.Core.Cameras;

/// <summary>
/// 与厂商 SDK 无关的一帧图像数据。
/// </summary>
/// <param name="Width">图像宽度，单位为像素。</param>
/// <param name="Height">图像高度，单位为像素。</param>
/// <param name="PixelFormat">与驱动无关的像素格式名称。</param>
/// <param name="Data">图像的托管内存数据。</param>
public sealed record ImageFrame(
    int Width,
    int Height,
    string PixelFormat,
    ReadOnlyMemory<byte> Data);
