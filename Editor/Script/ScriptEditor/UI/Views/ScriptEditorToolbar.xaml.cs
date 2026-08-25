using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScriptEditor.Views;

/// <summary>
/// 脚本编辑器工具栏（前后端分离：从 ScriptEditorView 独立出来，延迟创建）。
/// </summary>
public partial class ScriptEditorToolbar : UserControl
{
    public ScriptEditorToolbar()
    {
        InitializeComponent();
    }

    private void LoopInterval_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void LoopInterval_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var text = (string)e.DataObject.GetData(typeof(string));
        if (!int.TryParse(text, out _))
        {
            e.CancelCommand();
        }
    }
}