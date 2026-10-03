// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 直接导入的协作命名空间包括：System.Runtime.CompilerServices、WPFCustomControls.PropertyGrid.Core.Composition、WPFCustomControls.PropertyGrid.Core.Models、WPFCustomControls.PropertyGrid.Core.Services、WPFCustomControls.PropertyGrid.Services、WPFCustomControls.PropertyGrid.Core.ViewModels。导入不代表所有类型都会在运行时加载。
using System.Runtime.CompilerServices;
using WPFCustomControls.PropertyGrid.Core.Composition;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.Services;
using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

// Preserve binary compatibility for consumers compiled before PropertyGrid moved
// from WPFCustomControls.dll into WPFCustomControls.PropertyGrid.Core.dll.
[assembly: TypeForwardedTo(typeof(IUiDispatcher))]
[assembly: TypeForwardedTo(typeof(UiDispatcher))]
[assembly: TypeForwardedTo(typeof(NullUiDispatcher))]
[assembly: TypeForwardedTo(typeof(EnumOption))]
[assembly: TypeForwardedTo(typeof(UpstreamNodeInfo))]
[assembly: TypeForwardedTo(typeof(DataBindingInfo))]
[assembly: TypeForwardedTo(typeof(IDataBindingProvider))]
[assembly: TypeForwardedTo(typeof(IPortConfigurable))]
[assembly: TypeForwardedTo(typeof(IPortManageable))]
[assembly: TypeForwardedTo(typeof(PortDialogRequest))]
[assembly: TypeForwardedTo(typeof(PortDialogResult))]
[assembly: TypeForwardedTo(typeof(IPortDialogService))]
[assembly: TypeForwardedTo(typeof(NodePropertyAttribute))]
[assembly: TypeForwardedTo(typeof(PropertyGridActionAttribute))]
[assembly: TypeForwardedTo(typeof(PropertyGridActionItem))]
[assembly: TypeForwardedTo(typeof(IPropertyGridBuilderService))]
[assembly: TypeForwardedTo(typeof(PropertyGridBuilderService))]
[assembly: TypeForwardedTo(typeof(PropertyItemViewModel))]
[assembly: TypeForwardedTo(typeof(PropertyGroupViewModel))]
[assembly: TypeForwardedTo(typeof(PropertyGridHostViewModel))]
[assembly: TypeForwardedTo(typeof(PortRowViewModel))]
[assembly: TypeForwardedTo(typeof(PortManagementViewModel))]
[assembly: TypeForwardedTo(typeof(InputPortEditViewModel))]
[assembly: TypeForwardedTo(typeof(OutputPortEditViewModel))]
