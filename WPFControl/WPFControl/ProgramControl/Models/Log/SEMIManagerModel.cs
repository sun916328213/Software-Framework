using CommunityToolkit.Mvvm.ComponentModel;
using Secs.Variable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFControl.ProgramControl.Models
{
    internal class SEMIManagerModel:ObservableObject
    {
        private ObservableCollection<ReportInfo> iReports;
        public ObservableCollection<ReportInfo> Reports
        {
            get => iReports;
            set => SetProperty(ref iReports, value);
        }

        private ObservableCollection<CEIDInfo> iEvents;
        public ObservableCollection<CEIDInfo> Events
        {
            get => iEvents;
            set => SetProperty(ref iEvents, value);
        }
    }
}
