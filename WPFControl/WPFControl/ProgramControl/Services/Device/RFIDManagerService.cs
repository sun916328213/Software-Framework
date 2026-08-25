using Device.RFID;
using Device.RFID.Definition;
using Extension.Core.ConvertEx;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class RFIDManagerService
    {
        public RFIDManagerModel Model { get; set; } = new RFIDManagerModel();

        internal RFIDManager Manager;

        public RFIDManagerService(IContainerProvider container)
        {
            TryCatchHelper.TryExecute(() =>
            {
                Manager = container.Resolve<RFIDManager>();
                var d1 = JsonEx.Read<ObservableCollection<RFIDParam>>(Model.FilePath, Model.FileName);
                Model.DataCollection = d1 ?? Model.DataCollection;
            });
        }

        public void SetCurRFID()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.CurRFID = Manager[Model.SelectedItem.Name];
                Model.ConnectStatus = Model.CurRFID.IsConnected == true ? "已连接" : "未连接";
            });
        }

        public void Connect()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.Error = "";
                Model.CurRFID.Connect();
                Model.ConnectStatus = Model.CurRFID.IsConnected == true ? "已连接" : "未连接";
            });
        }


        public void Write()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.Error = "";
                Model.CurRFID.Write(Convert.ToInt32(Model.Page), Model.WriteValue, 8);
            });
        }

        public void Read()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.Error = "";
                Model.ReadValue = Model.CurRFID.Read(Convert.ToInt32(Model.Page));
            });
        }

        public void Load()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.Error = "";
                Manager.Config (Model.DataCollection.ToList());
                Manager.Connect();
            });
        }
    }
}
