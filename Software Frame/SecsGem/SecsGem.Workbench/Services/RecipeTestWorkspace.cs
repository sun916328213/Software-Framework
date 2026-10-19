using SecsGem.Workbench.Models;

namespace SecsGem.Workbench.Services;

/// <summary>仅管理模拟宿主的本地配方切换；没有连接、序列化或协议订阅。</summary>
public sealed class RecipeTestWorkspace
{
    public RecipeTestModel Model { get; } = new();

    public void SwitchRecipe()
    {
        if (!Model.CanSwitchRecipe(Model.SelectedPpid) || !Model.Recipes.TryGetValue(Model.SelectedPpid, out var recipe))
        {
            Model.Status = "请选择解析成功的配方；解析失败的正文不能切换。";
            return;
        }
        Model.CurrentRecipe = recipe;
        Model.CurrentPpid = Model.SelectedPpid;
        Model.Status = $"当前模拟配方：{Model.CurrentPpid}；没有发送报文或操作真实设备。";
    }
}
