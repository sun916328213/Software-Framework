namespace Vision.Tools.Execution;

/// <summary>
/// 上下文独占的原生模型资源。释放失败时所有权不能丢失，引擎会保留并允许关闭重试。
/// </summary>
internal interface IPreparedVisionModel : IDisposable
{
}
