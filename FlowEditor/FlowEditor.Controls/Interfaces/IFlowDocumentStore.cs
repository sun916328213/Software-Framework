namespace FlowEditor.Interfaces;

/// <summary>流程文档持久化 seam，隐藏文档介质、序列化格式与目录创建细节。</summary>
public interface IFlowDocumentStore
{
    /// <summary>将文档保存到指定位置。</summary>
    /// <typeparam name="TDocument">流程文档根类型。</typeparam>
    /// <param name="path">文档位置。</param>
    /// <param name="document">待保存文档。</param>
    void Save<TDocument>(string path, TDocument document) where TDocument : class;

    /// <summary>从指定位置加载文档。</summary>
    /// <typeparam name="TDocument">流程文档根类型。</typeparam>
    /// <param name="path">文档位置。</param>
    /// <returns>反序列化后的文档。</returns>
    TDocument Load<TDocument>(string path) where TDocument : class;
}
