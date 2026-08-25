using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Ioc;
using Prism.Regions;
using System;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    internal class LightControlManagerViewModel : ObservableObject
    {
        private readonly LightControlManagerService Service;
        private readonly IRegionManager _regionManager;

        public LightControlManagerModel Datas => Service.Model;

        public Action SaveCallbackCommand { get; }

        public LightControlManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<LightControlManagerService>();
            _regionManager = container.Resolve<IRegionManager>();

            SaveCallbackCommand = new Action(() =>
            {
                Service.Load();
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _regionManager.Regions["LightControlRegion"].RemoveAll();
                    _regionManager.RequestNavigate("LightControlRegion", "LightControlView");
                });
            });
        }
    }
}