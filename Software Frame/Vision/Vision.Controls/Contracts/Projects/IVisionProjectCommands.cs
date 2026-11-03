using System.ComponentModel;
using System.Windows.Input;

#nullable enable
namespace Vision.Controls.Contracts;

/// <summary>Controls工程文件操作能力；编辑视图借用命令，不要求工具目录或取得工作区所有权。</summary>
public interface IVisionProjectCommands : INotifyPropertyChanged
{
    string? CurrentProjectPath { get; }
    string ProjectTitle { get; }
    ICommand ImportProjectCommand { get; }
    ICommand SaveProjectCommand { get; }
    ICommand SaveProjectAsCommand { get; }
    ICommand LoadImageCommand { get; }
}
