using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecsGem.Workbench.Models;
using SecsGem.Workbench.Services;

namespace SecsGem.Workbench.ViewModels;

public partial class RecipeTestViewModel : ObservableObject, IDisposable
{
    private readonly RecipeTestWorkspace _workspace;
    private bool _disposed;
    public RecipeTestModel Model => _workspace.Model;
    public RecipeTestViewModel(RecipeTestWorkspace workspace)
    {
        _workspace = workspace;
        PropertyChangedEventManager.AddHandler(Model, OnModelChanged, string.Empty);
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => SwitchRecipeCommand.NotifyCanExecuteChanged();
    private bool CanSwitch() => !_disposed && Model.CanSwitchRecipe(Model.SelectedPpid);
    [RelayCommand(CanExecute = nameof(CanSwitch))]
    private void SwitchRecipe() { if (CanSwitch()) _workspace.SwitchRecipe(); }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PropertyChangedEventManager.RemoveHandler(Model, OnModelChanged, string.Empty);
        SwitchRecipeCommand.NotifyCanExecuteChanged();
    }
}
