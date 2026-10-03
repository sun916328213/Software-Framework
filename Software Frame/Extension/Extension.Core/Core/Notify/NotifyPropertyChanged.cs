// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：NotifyPropertyChanged。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;

namespace Extension.Core.Notify
{
    /// <summary>
    /// NotifyPropertyChanged 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// 继承或实现的契约：INotifyPropertyChanged。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class NotifyPropertyChanged : INotifyPropertyChanged
    {
    	public event PropertyChangedEventHandler PropertyChanged;

    	/// <summary>
    	/// 处理对应通知、事件或生命周期回调；对应入口为 OnPropertyChanged。
    	/// </summary>
    	protected virtual void OnPropertyChanged(string propertyName )
    	{
    		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    	}
    }
}
