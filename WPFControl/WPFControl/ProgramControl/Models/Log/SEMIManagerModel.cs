using CommunityToolkit.Mvvm.ComponentModel;
using SecsGem.Core;
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
        private ObservableCollection<SecsGemReportDefinition> iReports;
        public ObservableCollection<SecsGemReportDefinition> Reports
        {
            get => iReports;
            set => SetProperty(ref iReports, value);
        }

        private ObservableCollection<SecsGemEventDefinition> iEvents;
        public ObservableCollection<SecsGemEventDefinition> Events
        {
            get => iEvents;
            set => SetProperty(ref iEvents, value);
        }
    }
}
