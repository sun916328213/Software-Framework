using Prism.Ioc;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    public class ResourceMonitorViewModel
    {
        readonly ResourceMonitorService Service;

        public ResourceMonitorModel Datas => Service.Model;

        public ResourceMonitorViewModel(IContainerProvider container)
        {
            Service=container.Resolve<ResourceMonitorService>();
        }
    }
}
