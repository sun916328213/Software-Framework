using System.Windows.Controls;
using SecsGem.Workbench.Services;
using SecsGem.Workbench.ViewModels;

namespace SecsGem.Workbench.Views;

/// <summary>仅提供本地手动切换；页面只释放自己的 VM，不拥有共享配方数据。</summary>
public partial class RecipeTestView : UserControl, IDisposable
{
    private readonly RecipeTestViewModel _viewModel;
    public RecipeTestView(RecipeTestWorkspace workspace) : this(new RecipeTestViewModel(workspace)) { }
    public static RecipeTestView Create(RecipeTestViewModel viewModel) => new(viewModel);
    private RecipeTestView(RecipeTestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }
    public void Dispose() => _viewModel.Dispose();
}
