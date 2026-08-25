using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extension.DesignPattern.ObserverPattern;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Services;

namespace WPFControl.CustomControl.ViewModels
{
    internal class LightControlViewModel: ObservableObject
    {
        LightControlService Service { get; set; }

        string _SelectLightControlName;int _SelectChannel, _LightIntensity;

        public List<string> LightControlNames => Service.LightControlManager.DeviceNameCollection();


        public string SelectLightControlName
        {
            get => _SelectLightControlName;
            set=> SetProperty(ref _SelectLightControlName, value);
        }
        public int SelectChannel
        { 
            get => _SelectChannel;
            set=> SetProperty(ref _SelectChannel, value);
        }

        public int LightIntensity
        { 
         get => _LightIntensity;
            set=> SetProperty(ref _LightIntensity, value);
        }

        public RelayCommand  SetLightCommand { get; }

        public RelayCommand OpenAllCommand { get; }

        public RelayCommand CloseAllCommand { get; }



        public LightControlViewModel(IContainerProvider container)
        {
            Service = container.Resolve<LightControlService>();

            SetLightCommand = new RelayCommand(() =>
                Service.SetLight(SelectLightControlName, LightIntensity, SelectChannel)
            );

            OpenAllCommand = new RelayCommand(() =>
                Service.OpenAll(SelectLightControlName, LightIntensity)
            );

            CloseAllCommand = new RelayCommand(() =>
                Service.CloaseAll(SelectLightControlName)
            );
        }
    }
}