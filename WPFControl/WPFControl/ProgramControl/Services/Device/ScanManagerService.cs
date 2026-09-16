using Device.Light;
using Device.Light.Definition;
using Device.RFID;
using Device.RFID.Definition;
using Device.Scan;
using Device.Scan.Definition;
using Extension.Core.ConvertEx;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControl.CustomControl.Services;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class ScanManagerService
    {
        public ScanManagerModel Model { get; set; } = new   ScanManagerModel();

        internal ScanManager Manager;

        public ScanManagerService(IContainerProvider container)
        {
            TryCatchHelper.TryExecute(() =>
            {
                Manager = container.Resolve<ScanManager>();
                var d1 = JsonEx.Read<ObservableCollection<ScanParam>>(Model.FilePath, Model.FileName);
                Model.DataCollection = d1 ?? Model.DataCollection;
            });
        }

        public void Load()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Manager.Disconnect();
                Manager.Config(Model.DataCollection.ToList());
                Manager.Connect();
            });
        }
    }
}

