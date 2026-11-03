#nullable enable

namespace Vision.Core.Operations;

/// <summary>前景连通性；填孔时背景使用对应的互补连通性。</summary>
public enum BlobConnectivity
{
    /// <summary>仅共享边连通。</summary>
    Four = 4,

    /// <summary>共享边或顶点均连通。</summary>
    Eight = 8
}
