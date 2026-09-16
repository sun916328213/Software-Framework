using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extension.Core.ConvertEx;
using Prism.Ioc;
using Secs;
using Secs.Variable;
using System;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    internal class SEMIManagerViewModel : ObservableObject
    {

        public SEMIManagerService Service { get; set; }
        public SEMIManagerModel Datas => Service.Model;

        public RelayCommand LoadCommand { get; }

        public Action ReportSaveCommand { get; }

        public Action EventSaveCommand { get; }

        public SEMIManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<SEMIManagerService>();

            LoadCommand = new RelayCommand(() =>
            {
                Service.ReportLoad();
                Service.EventLoad();
            });
            ReportSaveCommand =()=> Service.ReportSave();
            EventSaveCommand = () => Service.EventSave();
        }
    }
}

