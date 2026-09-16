using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Log.Definition;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    public class LogAlarmInfoViewModel:ObservableObject
    {
        readonly LogAlarmInfoService Service;

        public ObservableCollection<LogInfo> ItemsSource => Service.Model.DataCollection;

        public LogAlarmInfoViewModel(LogAlarmInfoService model)
        {
            Service = model;
        }
    }
}