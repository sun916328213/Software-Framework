using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Device.PrintSoftware;
using System;
using WPFControl.CustomControl.Models;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    class PrintManagerViewModel 
    {
        private PrintManagerService Service;

        public PrintManagerModel Datas => Service.Model;

        public RelayCommand<PrintInfo> AddCommand { get; }
        public RelayCommand SettingCommand { get; }

        public RelayCommand PrintCommand { get; }
        public RelayCommand ClearCommand { get; }

        public RelayCommand UpCommand { get; }

        public RelayCommand DownCommand { get; }

        public PrintManagerViewModel(PrintManagerService model)
        {
            Service = model;

            AddCommand = new RelayCommand<PrintInfo>((s) =>
           {
               Service.Add(s);
           });

            ClearCommand = new RelayCommand(() =>
            {
                Service.RemoveAt();
            });

            UpCommand = new RelayCommand(() =>
            {
                Service.Up();
            });

            DownCommand = new RelayCommand(() =>
            {
                Service.Down();
            });

            SettingCommand = new RelayCommand(() =>
            {
                Service.RemoveAll();
            });
            PrintCommand = new RelayCommand(() =>
             {
                 Service.TestPrint();
             });
        }
    }
}
