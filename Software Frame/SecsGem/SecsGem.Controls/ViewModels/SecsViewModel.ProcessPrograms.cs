using CommunityToolkit.Mvvm.Input;

namespace SecsGem.Controls.ViewModels;

public partial class SecsViewModel
{
    private bool CanImportHostRecipes() => CanUseWorkspace() && Model.HasRecipeSource && !Model.IsConnected;
    private bool CanUseHostRecipe() => CanUseWorkspace() && Model.HasRecipeSource;
    private bool CanCopyRecipeRequest() => CanUseWorkspace() && !string.IsNullOrWhiteSpace(Model.RecipeRequestSml);

    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private void RefreshProcessPrograms() { if (CanUseWorkspace()) Workspace.RefreshProcessPrograms(); }
    [RelayCommand(CanExecute = nameof(CanImportHostRecipes))]
    private void ImportHostRecipes() { if (CanImportHostRecipes()) Workspace.ImportHostRecipes(); }
    [RelayCommand(CanExecute = nameof(CanUseHostRecipe))]
    private void UseCurrentRecipePpid() { if (CanUseHostRecipe()) Workspace.UseCurrentRecipePpid(); }
    [RelayCommand(CanExecute = nameof(CanUseHostRecipe))]
    private void ReadRecipeToHost() { if (CanUseHostRecipe()) Workspace.ReadRecipeToHost(); }
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private void BuildRecipeRequest() { if (CanUseWorkspace()) Workspace.BuildRecipeRequest(); }
    [RelayCommand(CanExecute = nameof(CanCopyRecipeRequest))]
    private void CopyRecipeRequest() { if (CanCopyRecipeRequest()) Workspace.CopyRecipeRequest(); }

    private void RefreshRecipeCommands()
    {
        RefreshProcessProgramsCommand.NotifyCanExecuteChanged();
        ImportHostRecipesCommand.NotifyCanExecuteChanged();
        UseCurrentRecipePpidCommand.NotifyCanExecuteChanged();
        ReadRecipeToHostCommand.NotifyCanExecuteChanged();
        BuildRecipeRequestCommand.NotifyCanExecuteChanged();
        CopyRecipeRequestCommand.NotifyCanExecuteChanged();
    }
}
