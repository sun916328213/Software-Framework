namespace CommunicationNew.Abstractions;

/// <summary>
/// 设备侧 32 位数据的字节与字顺序。
/// 字母按标准大端值从最高有效字节到最低有效字节命名。
/// </summary>
public enum DataFormat
{
    ABCD,
    BADC,
    CDAB,
    DCBA,
}
