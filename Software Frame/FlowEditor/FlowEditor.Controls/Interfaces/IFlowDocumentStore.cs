// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IFlowDocumentStore。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
namespace FlowEditor.Controls.Interfaces;

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
