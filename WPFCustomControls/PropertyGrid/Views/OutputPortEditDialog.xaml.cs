using System.Windows;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.PropertyGrid.Views;

public partial class OutputPortEditDialog : System.Windows.Window
{
    public OutputPortEditDialog(OutputPortEditViewModel viewModel)
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