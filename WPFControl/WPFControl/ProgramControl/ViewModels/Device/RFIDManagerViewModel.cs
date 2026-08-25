using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using System;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    internal class RFIDManagerViewModel : ObservableObject
    {
        private readonly RFIDManagerService Service;


        public RFIDManagerModel Datas => Service.Model;



        public Action SaveCallbackCommand { get; }

        public Action SelectCallbackCommand { get; }

        public RelayCommand ConnectCommand { get; }
        public RelayCommand ReadCommand { get; }
        public RelayCommand WriteCommand { get; }

        public RFIDManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<RFIDManagerService>();

            ConnectCommand = new RelayCommand(() =>
            {
                Service.Connect();
            });

            WriteCommand = new RelayCommand(() =>
            {
                Service.Write();
            });

            ReadCommand = new RelayCommand(() =>
            {
                Service.Read();
            });

            SaveCallbackCommand = (() =>
            {
                Service.Load();
            });
            SelectCallbackCommand = (() =>
            {
                Service.SetCurRFID();
            });
        }
    }
}