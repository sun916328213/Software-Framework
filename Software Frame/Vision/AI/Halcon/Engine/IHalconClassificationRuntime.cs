using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.AI.Halcon.Engine;

// 仅SDK边界测试缝；不进入Core/业务，也不转发旧Provider或Session。
internal interface IHalconClassificationRuntime : IDisposable
{
    Exception? CleanupFailure
    {
        get;
    }
    IHalconClassificationModel Load(string path, string deviceId);
}

internal interface IHalconClassificationModel : IPreparedVisionModel
{
    int Width
    {
        get;
    }
    int Height
    {
        get;
    }
    IReadOnlyList<AiClassPrediction> Run(VisionFrame image);
}
