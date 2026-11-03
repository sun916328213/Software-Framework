#nullable enable

namespace Vision.Core.Results;

/// <summary>
/// 完成的毫米长度值契约，不是第二执行接口。CoordinateSpaceId始终为来源像素空间；
/// 世界空间/参考依据由具体标定快照携带，不能凭共同单位混合不同标定。
/// </summary>
public interface IMillimeterLengthMeasurement
{
    Guid FrameId
    {
        get;
    }
    Guid CoordinateSpaceId
    {
        get;
    }
    Guid CalibrationId
    {
        get;
    }
    double ValueMillimeters
    {
        get;
    }
}
