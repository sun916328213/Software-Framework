using Prism.Ioc;
using Secs;
using Secs.Variable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    internal class SEMIManagerService
    {
        public SEMIManagerModel Model { get; set; } = new SEMIManagerModel();

        private SecsControl SecsControl;

        public SEMIManagerService(IContainerProvider container)
        {
            SecsControl = container.Resolve<SecsControl>();

         
         
        }

        public void ReportLoad()
        {
            SecsControl._VIDManager.ReportID.Load();
            Model.Reports = new ObservableCollection<ReportInfo>(SecsControl._VIDManager.ReportID.GetDatas());
        }

        public void EventLoad()
        {
            SecsControl._VIDManager.CEID.Load();
            Model.Events = new ObservableCollection<CEIDInfo>(SecsControl._VIDManager.CEID.GetDatas());
        }

        public void ReportSave() => SecsControl._VIDManager.ReportID.Save();
        public void EventSave() => SecsControl._VIDManager.CEID.Save();

    }
}
