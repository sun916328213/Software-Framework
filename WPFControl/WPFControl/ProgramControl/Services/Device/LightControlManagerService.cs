using Device.Light;
using Device.Light.Definition;
using Device.RFID;
using Device.RFID.Definition;
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
    public class LightControlManagerService
    {
        public LightControlManagerModel Model { get; set; } = new  LightControlManagerModel();

        internal LightControlManager Manager;

        public LightControlManagerService(IContainerProvider container)
        {
            TryCatchHelper.TryExecute(() =>
            {
                Manager = container.Resolve<LightControlManager>();
                var d1 = JsonEx.Read<ObservableCollection<LightParam>>(Model.FilePath, Model.FileName);
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
