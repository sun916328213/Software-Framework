using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Struct;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    public class LogInfoViewModel:ObservableObject
    {
        readonly LogInfoService Service;

        public ObservableCollection<LogInfo> ItemsSource => Service.Model.DataCollection;

        public LogInfoViewModel(LogInfoService model)
        {
            Service = model;
        }
    }
}