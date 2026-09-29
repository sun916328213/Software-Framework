using System.Collections.Generic;
using System;
using Microsoft.CodeAnalysis;
using ScriptEditor.Models;

namespace ScriptEditor.Services;

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

    public void ClearReferences()
    {
        _model.ClearReferenceData();
        _additionalReferences.Clear();
        _localInjectedReferencePaths.Clear();
        SyncWorkspaceReferences();
        SyncAttachedItemReferences();
    }

    public IReadOnlyList<MetadataReference> GetAdditionalReferences() => _additionalReferences;

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
