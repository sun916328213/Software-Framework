using CommunityToolkit.Mvvm.ComponentModel;
using WPFControl.ProgramControl.Services;
using System.Collections.ObjectModel;

namespace WPFControl.ProgramControl.ViewModels
{
    public class SEMILogViewModel: ObservableObject
    {
        readonly SEMILogService Service;

        public ObservableCollection<string> ItemsSource => Service.Model.DataCollection;

        public SEMILogViewModel(SEMILogService model)
        {
            Service = model;
        }
    }
}
