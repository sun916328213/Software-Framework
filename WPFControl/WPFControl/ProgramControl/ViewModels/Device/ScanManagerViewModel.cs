using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Ioc;
using Prism.Regions;
using System;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    internal class ScanManagerViewModel : ObservableObject
    {
        private readonly ScanManagerService Service;
      
        public ScanManagerModel Datas => Service.Model;

        public Action SaveCallbackCommand { get; }

        public ScanManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<ScanManagerService>();
         
            SaveCallbackCommand = new Action(() =>
            {
                Service.Load();
            });
        }
    }
}