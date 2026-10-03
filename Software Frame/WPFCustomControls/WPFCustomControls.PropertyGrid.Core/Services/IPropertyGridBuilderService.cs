// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：IPropertyGridBuilderService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls.PropertyGrid.Core.Models、WPFCustomControls.PropertyGrid.Core.ViewModels。导入不代表所有类型都会在运行时加载。
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace WPFCustomControls.PropertyGrid.Core.Services;

/// <summary>
/// 属性网格构建服务。
/// 负责通过反射扫描目标对象的 [NodeProperty] 特性，产出分组 ViewModel 树，
/// 供 <see cref="ViewModels.PropertyGridHostViewModel"/> 绑定渲染。
///
/// 该接口纯业务逻辑、无 UI 依赖（MVVM：业务逻辑下沉到 Service 层，View 只负责渲染）。
/// </summary>
public interface IPropertyGridBuilderService
{
    /// <summary>
    /// 扫描目标对象的 [NodeProperty] 属性，按 Group 分组并排序，产出分组 VM 列表。
    /// </summary>
    /// <param name="target">被编辑的目标对象（如 NodeModel）。</param>
    /// <param name="portConfig">目标对象实现的端口配置接口（可为空，用于内联端口开关）。</param>
    /// <returns>分组 ViewModel 列表；目标为空时返回空列表。</returns>
    IReadOnlyList<PropertyGroupViewModel> BuildGroups(object? target, IPortConfigurable? portConfig);

    /// <summary>
    /// 收集目标类型中可绑定到自定义输出端点的属性名（仅含 public setter，排除流/端口相关属性）。
    /// </summary>
    List<string> CollectBindableProperties(Type type);
}