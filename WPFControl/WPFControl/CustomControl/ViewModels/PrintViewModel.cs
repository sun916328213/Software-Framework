using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Services;

namespace WPFControl.CustomControl.ViewModels
{
    internal class PrintViewModel : ObservableObject
    {
        PrintService Service { get; set; }

        public PrintModel Datas => Service.Model;


        public RelayCommand<string> SelectPrintCommand { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand GetValueCommand { get; }
        public RelayCommand SetValueCommand { get; }
        public RelayCommand PrintCommand { get; }

        public PrintViewModel(IContainerProvider container)
        {
            Service = container.Resolve<PrintService>();
            SelectPrintCommand = new RelayCommand<string>((s) =>
            {
               Service.SetPrinterName(s);
            });
            OpenCommand = new RelayCommand(async () =>
            {
                await Service.OpenFile();
            });
            GetValueCommand = new RelayCommand(() =>
            {
                Service.GetKey();
            });
            SetValueCommand = new RelayCommand(() =>
            {
                Service.SetValue();
            });

            PrintCommand = new RelayCommand(async () =>
            {
                await Service.Print();
            });
        }
    }
}