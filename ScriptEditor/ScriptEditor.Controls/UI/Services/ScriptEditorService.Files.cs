using System;
using System.Collections.Generic;
using System.IO;
using Extension.Core.Conversion;
using ScriptEditor.Models;

namespace ScriptEditor.Services;

/// <summary>
/// ScriptEditorService —— 文件操作模块。
/// 负责脚本的新建/打开/保存/另存为、ScriptEditorState JSON 持久化与默认脚本模板。
/// </summary>
public partial class ScriptEditorService
{
    // ════════════════════════════════════════════════════════════════
    //  默认脚本模板
    // ════════════════════════════════════════════════════════════════

    public string GetDefaultTemplate()
    {
        return @"
public class Script
{
    public void Initialize()
    {
        // 初始化逻辑（可选，不需要可删除）
    }

    public void Run()
    {
        Console.WriteLine(""开始执行脚本"");
    }
}";
    }

    // ════════════════════════════════════════════════════════════════
    //  文件操作
    // ════════════════════════════════════════════════════════════════

    #region 文件操作

    /// <summary>新建文档。</summary>
    public void NewDocument()
    {
        _model.CurrentFilePath = null;
        SetText(GetDefaultTemplate());
        _model.DocumentTitle = "无标题";
        _session.SetOutput(string.Empty);
        _model.StatusText = "已创建新文档";
        _model.HasModified = false;
    }

    /// <summary>打开文件对话框并加载脚本。</summary>
    public void OpenDocument()
    {
        var path = _fileService.ShowOpenDialog(
            "脚本工程文件 (*.scriptproj;*.scriptproj.bak)|*.scriptproj;*.scriptproj.bak|C# 文件 (*.cs;*.cs.bak)|*.cs;*.cs.bak|脚本文件 (*.csx)|*.csx|所有文件 (*.*)|*.*");
        if (path == null) return;

        LoadScriptCore(path);
    }

    /// <summary>保存文件（有路径则直接保存，否则另存为）。</summary>
    public void Save()
    {
        if (_model.CurrentFilePath != null)
            SaveToFile(_model.CurrentFilePath);
        else
            SaveAsCore();
    }

    /// <summary>另存为。</summary>
    public void SaveAs()
    {
        SaveAsCore();
    }

    private void SaveAsCore()
    {
        var path = _fileService.ShowSaveDialog(
            "脚本工程文件 (*.scriptproj)|*.scriptproj|C# 文件 (*.cs)|*.cs|脚本文件 (*.csx)|*.csx|所有文件 (*.*)|*.*",
            _model.DocumentTitle);
        if (path == null) return;
        SaveToFile(path);
    }

    private void SaveToFile(string path)
    {
        try
        {
            var state = new ScriptEditorState
            {
                Code = GetText(),
                References = AdditionalReferencePaths.ToList()
            };

            var json =   JsonEx.ToJson(state);
            _fileService.WriteAllText(path, json);
            _model.CurrentFilePath = path;
            _model.DocumentTitle = Path.GetFileName(path);
            _model.StatusText = $"已保存: {path}";
            _model.HasModified = false;
        }
        catch (Exception ex)
        {
            _session.SetOutput($"保存文件失败: {ex.Message}");
            _model.StatusText = "保存失败";
        }
    }

    /// <summary>从指定路径加载脚本文件（支持 JSON 格式和旧纯文本格式）。</summary>
    public void LoadFromFile(string path)
    {
        LoadScriptCore(path);
    }

    /// <summary>核心加载逻辑：JSON 格式优先，回退到纯文本。</summary>
    private void LoadScriptCore(string path)
    {
        try
        {
            var content = _fileService.ReadAllText(path);

            // 尝试解析为 JSON 格式（ScriptEditorState）
            var state = TryParseScriptEditorState(content);
            if (state != null)
            {
                SetText(state.Code);

                // 加载 DLL 引用：直接操作 Model 数据层，最后一次性同步工作区
                ClearReferences();
                foreach (var refPath in state.References)
                    AddReference(refPath);
                _localInjectedReferencePaths.Clear();
                SyncWorkspaceReferences();
                SyncAttachedItemReferences();
                LoadReferences();
            }
            else
            {
                // 旧格式：纯文本代码
                SetText(content);
            }

            _model.CurrentFilePath = path;
            _model.DocumentTitle = Path.GetFileName(path);
            _session.SetOutput(string.Empty);
            _model.StatusText = $"已打开: {path}";
            _model.HasModified = false;

            if (IsInitialized && !string.IsNullOrEmpty(GetText()))
                _ = PreCompileScriptAsync(GetText());
        }
        catch (Exception ex)
        {
            _session.SetOutput($"打开文件失败: {ex.Message}");
            _model.StatusText = "打开失败";
        }
    }

    /// <summary>尝试将文件内容解析为 ScriptEditorState JSON，失败返回 null。</summary>
    private static ScriptEditorState? TryParseScriptEditorState(string content)
    {
        try
        {
            var state = JsonEx.FromJson<ScriptEditorState>(content);
            // 有效 JSON 必须包含 Code 字段（即使为空字符串）
            return state?.Code != null ? state : null;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
