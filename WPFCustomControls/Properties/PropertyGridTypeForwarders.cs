using System.Runtime.CompilerServices;
using WPFCustomControls.PropertyGrid.Composition;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.ViewModels;

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
