using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace WPFCustomControls.Controls;

/// <summary>
/// 通用配置管理模型（随 ConfigManagerView 一起走）。
/// 承载 FilePath/FileName/Name/DataCollection，ConfigManagerView 的宿主 ViewModel 继承它。
/// 由 ManagerModel 重命名而来，去设备化，可复用于用户/配方/参数等配置表。
/// </summary>
public class ConfigManagerModel<T> : ObservableObject
{
    string? filepath, filename, devicename;
    /// <summary>
    /// 路径
    /// </summary>
    public   string? FilePath
    {
        get => filepath;
        set => SetProperty(ref filepath, value);
    }
    /// <summary>
    /// 用户信息文件路径
    /// </summary>
    public   string?  FileName
    {
        get => filename;
        set => SetProperty(ref filename, value);
    }

    /// <summary>
    /// 配置名称 
    /// </summary>
    public   string? Name
    {
        get => devicename;
        set => SetProperty(ref devicename, value);
    }

    private ObservableCollection<T> dataCollection = [];

    /// <summary>
    /// 数据集合
    /// </summary>
    public ObservableCollection<T> DataCollection
    {
        get => dataCollection;
        set => SetProperty(ref dataCollection, value);
    }

    /// <summary>
    /// 完整路径（FilePath + FileName 组合；任一缺失回 null，调用方判空）。
    /// </summary>
    public string? FullPath =>
        string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(FileName)
            ? null
            : Path.Combine(FilePath, FileName);
}
 
