namespace FlowEditor.Interfaces;

/// <summary>
/// 将需要更新宿主界面的动作封送到 UI 线程的 seam。
/// 核心应用服务只依赖该契约，不感知 WPF 的 Application 或 Dispatcher。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>在宿主 UI 线程执行指定动作。</summary>
    /// <param name="action">需要在 UI 线程执行的动作。</param>
    void Invoke(Action action);
}
