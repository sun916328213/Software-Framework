// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：SecsWorkbenchViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls.Services、SecsGem.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls.Services;
using SecsGem.Controls.ViewModels;

namespace SecsGem.Workbench.ViewModels;

/// <summary>保留工作台绑定类型；页面状态和命令由 Controls 提供。</summary>
public sealed class SecsWorkbenchViewModel(SecsWorkspace workspace) : SecsViewModel(workspace);
