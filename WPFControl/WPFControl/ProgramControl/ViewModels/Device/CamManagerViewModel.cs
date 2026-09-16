using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using System;
using WPFControl.CustomControl.Views.Display;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    public class CamManagerViewModel : ObservableObject
    {
        private readonly CamManagerService Service;

        public CamManagerModel Datas => Service.Model;
 
        public Action SaveCallbackCommand { get; }

        public Action SelectCallbackCommand { get; }

        public RelayCommand ConnectCommand { get; }
        public RelayCommand GrabCommand { get; }
        public RelayCommand LiveCommand { get; }
        public RelayCommand<DisplaySingleGroupView> ShowLineCommand { get; }
        public RelayCommand SettingCommand { get; }
        public RelayCommand SaveCommand { get; }

        public CamManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<CamManagerService>();

            ConnectCommand = new RelayCommand(() =>
            {
                Service.Connect();
            });

            GrabCommand = new RelayCommand(() =>
            {
                Service.Grab();
            });

            LiveCommand = new RelayCommand(() =>
            {
                Service.Live();
            });

            ShowLineCommand = new RelayCommand<DisplaySingleGroupView>(view =>
            {
                Service.ShowCross(view.HalconWindow);
            });

            SettingCommand = new RelayCommand(() =>
            {
                Service.SetParam();
                Service.Grab();
            });

            SaveCommand = new RelayCommand(() =>
            {
                Service.Save();
            });

            SaveCallbackCommand = (() =>
            {
                Service.LoadAsync();
            });

            SelectCallbackCommand = (() =>
            {
                Service.SetCurCam();
            });


        }
    }
}