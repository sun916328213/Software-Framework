using System.Windows;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.PropertyGrid.Views;

public partial class InputPortEditDialog : System.Windows.Window
{
    public InputPortEditDialog(InputPortEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(bool? dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}
