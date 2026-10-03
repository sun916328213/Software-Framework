// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptEditorService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、ScriptEditor.Runtime.Services、System.Collections.Generic、System、Microsoft.CodeAnalysis、ScriptEditor.Controls.Models。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using ScriptEditor.Runtime.Services;
using System.Collections.Generic;
using System;
using Microsoft.CodeAnalysis;
using ScriptEditor.Controls.Models;
using ScriptEditor.Core.Models;

namespace ScriptEditor.Controls.Services;

/// <summary>
/// ScriptEditorService —— 引用管理模块。
/// 负责 DLL 引用的增删改查、Roslyn 工作区引用同步，以及引用面板的 UI 操作。
/// </summary>
public partial class ScriptEditorService
{
    // ════════════════════════════════════════════════════════════════
    //  引用管理（Roslyn 工作区同步）
    // ════════════════════════════════════════════════════════════════

    public bool AddReference(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath) || !System.IO.File.Exists(dllPath) ||
            _model.AdditionalReferencePaths.Contains(dllPath, StringComparer.OrdinalIgnoreCase)) return false;
        MetadataReference reference;
        try { reference = MetadataReference.CreateFromFile(dllPath); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or BadImageFormatException) { return false; }
        if (!_model.AddReferenceData(dllPath)) return false;
        _additionalReferences.Add(reference);
        SyncWorkspaceReferences();
        SyncAttachedItemReferences();
        return true;
    }

    /// <summary>
    /// 执行移除操作；对应入口为 RemoveReference。
    /// 正文中的关键协作调用包括 FindIndex、Equals、RemoveReferenceData、RemoveAt、Remove；具体条件和先后次序见方法体。
    /// </summary>
    public bool RemoveReference(string dllPath)
    {
        var index = _model.AdditionalReferencePaths.FindIndex(path =>
            string.Equals(path, dllPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        if (!_model.RemoveReferenceData(dllPath)) return false;
        _additionalReferences.RemoveAt(index);
        _localInjectedReferencePaths.Remove(dllPath);
        SyncWorkspaceReferences();
        SyncAttachedItemReferences();
        return true;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ClearReferences。
    /// 正文中的关键协作调用包括 ClearReferenceData、Clear、SyncWorkspaceReferences、SyncAttachedItemReferences；具体条件和先后次序见方法体。
    /// </summary>
    public void ClearReferences()
    {
        _model.ClearReferenceData();
        _additionalReferences.Clear();
        _localInjectedReferencePaths.Clear();
        SyncWorkspaceReferences();
        SyncAttachedItemReferences();
    }

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetAdditionalReferences。
    /// </summary>
    public IReadOnlyList<MetadataReference> GetAdditionalReferences() => _additionalReferences;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SyncWorkspaceReferences。
    /// 正文中的关键协作调用包括 GetDocument、RemoveInjectedReferences、AddUserReferencesToProject、Clear、CollectReferencePaths；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void SyncWorkspaceReferences()
    {
        if (_host == null || _documentId == null || RoslynHost is not { } host) return;
        try
        {
            var document = _host.GetDocument(_documentId);
            if (document == null) return;
            var project = RemoveInjectedReferences(document.Project, _localInjectedReferencePaths);
            project = AddUserReferencesToProject(project, _additionalReferences);
            _localInjectedReferencePaths.Clear();
            foreach (var path in CustomRoslynHost.CollectReferencePaths(_additionalReferences))
                _localInjectedReferencePaths.Add(path);
            CommitWorkspaceChanges(project);
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// 执行移除操作；对应入口为 RemoveInjectedReferences。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、ResolveReferencePath、Equals、RemoveMetadataReference；具体条件和先后次序见方法体。
    /// </summary>
    private static Project RemoveInjectedReferences(Project project, HashSet<string> localInjectedPaths)
    {
        foreach (var injectedPath in localInjectedPaths)
        {
            foreach (var reference in project.MetadataReferences)
            {
                if (string.IsNullOrWhiteSpace(reference.Display)) continue;
                var resolvedPath = CustomRoslynHost.ResolveReferencePath(reference.Display);
                if (string.Equals(resolvedPath, injectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    project = project.RemoveMetadataReference(reference);
                    break;
                }
            }
        }
        return project;
    }

    /// <summary>
    /// 执行添加操作；对应入口为 AddUserReferencesToProject。
    /// 正文中的关键协作调用包括 CollectReferencePaths、IsNullOrWhiteSpace、ResolveReferencePath、Contains、AddMetadataReference；具体条件和先后次序见方法体。
    /// </summary>
    private static Project AddUserReferencesToProject(Project project, IReadOnlyList<MetadataReference> additionalReferences)
    {
        var existingPaths = CustomRoslynHost.CollectReferencePaths(project.MetadataReferences);
        foreach (var reference in additionalReferences)
        {
            if (string.IsNullOrWhiteSpace(reference.Display)) continue;
            var fullPath = CustomRoslynHost.ResolveReferencePath(reference.Display);
            if (!existingPaths.Contains(fullPath))
            {
                project = project.AddMetadataReference(reference);
                existingPaths.Add(fullPath);
            }
        }
        return project;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CommitWorkspaceChanges。
    /// 正文中的关键协作调用包括 GetDocument、TryApplyChanges、UpdateDocument；具体条件和先后次序见方法体。
    /// </summary>
    private void CommitWorkspaceChanges(Project project)
    {
        if (_host == null || _documentId == null) return;
        var updatedDoc = project.Solution.GetDocument(_documentId);
        if (project.Solution.Workspace.TryApplyChanges(project.Solution) && updatedDoc != null)
            _host.UpdateDocument(updatedDoc);
    }

    // ════════════════════════════════════════════════════════════════
    //  引用管理操作（引用面板 UI）
    // ════════════════════════════════════════════════════════════════

    /// <summary>从服务加载引用到 ObservableCollection。</summary>
    public void LoadReferences()
    {
        var names = RoslynHost?.DefaultReferences
            .Concat(CustomRoslynHost.GetAdditionalDefaultReferences())
            .Select(reference => reference.Display)
            .Where(display => !string.IsNullOrWhiteSpace(display))
            .Select(display => display!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(display => display, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
        _model.RefreshReferenceUI(names);
    }

    /// <summary>添加 DLL 引用（传入文件路径）。</summary>
    public void AddReferenceItem(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (AddReference(path))
        {
            _model.References.Add(ReferenceItem.FromPath(path));
            _model.StatusText = $"已添加引用: {path}";
        }
        else
        {
            _model.StatusText = "引用已存在或添加失败";
        }
    }

    /// <summary>打开对话框添加引用。</summary>
    public void AddReferenceFromDialog()
    {
        var path = _fileService.ShowOpenDialog("DLL 文件 (*.dll)|*.dll|所有文件 (*.*)|*.*");
        if (path == null) return;
        AddReferenceItem(path);
    }

    /// <summary>移除引用项。</summary>
    public void RemoveReferenceItem(ReferenceItem? item)
    {
        if (item == null) return;
        var isInService = AdditionalReferencePaths.Contains(item.FullPath, StringComparer.OrdinalIgnoreCase);
        if (isInService)
        {
            if (RemoveReference(item.FullPath))
            {
                _model.References.Remove(item);
                _model.StatusText = $"已移除引用: {item.Name}";
            }
            else
            {
                _model.StatusText = "移除引用失败";
            }
        }
        else
        {
            _model.References.Remove(item);
            _model.StatusText = $"已移除引用: {item.Name}";
        }
    }

    /// <summary>清空所有自定义引用。</summary>
    public void ClearAllReferenceItems()
    {
        ClearReferences();
        _model.References.Clear();
        LoadReferences();
        _model.StatusText = "已清空自定义引用";
    }

    /// <summary>切换引用面板展开状态。</summary>
    public void ToggleReferencePanel()
    {
        _model.IsReferencePanelOpen = !_model.IsReferencePanelOpen;
    }
}
