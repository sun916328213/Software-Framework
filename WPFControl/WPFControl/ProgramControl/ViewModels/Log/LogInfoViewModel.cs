using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Log.Definition;
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