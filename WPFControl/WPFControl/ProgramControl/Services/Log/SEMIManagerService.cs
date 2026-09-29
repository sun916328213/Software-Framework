using Prism.Ioc;
using SecsGem.Core;
using System;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    internal class SEMIManagerService
    {
        public SEMIManagerModel Model { get; set; } = new SEMIManagerModel();

        private readonly ISecsGemConfiguration _configuration;

        public SEMIManagerService(ISecsGemConfiguration configuration) => _configuration = configuration;

        public void ReportLoad()
        {
            Model.Reports = new ObservableCollection<SecsGemReportDefinition>(_configuration.LoadReports());
        }

        public void EventLoad()
        {
            Model.Events = new ObservableCollection<SecsGemEventDefinition>(_configuration.LoadEvents());
        }

        public void ReportSave() => _configuration.SaveReports();
        public void EventSave() => _configuration.SaveEvents();

    }
}
