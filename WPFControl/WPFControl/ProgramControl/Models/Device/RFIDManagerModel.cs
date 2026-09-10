using Device.RFID;
using Device.RFID.Definition;
using WPFControl.CustomControl.Models;
using WPFCustomControls.Controls;

namespace WPFControl.ProgramControl.Models
{
    public class RFIDManagerModel : ConfigManagerModel<RFIDParam>
    {
        public RFIDManagerModel()
        {
            Name = "RFID";
            FilePath = "Config//";
            FileName = "RFIDParam.json";
        }

        private IRFID _curRFID;
        public IRFID CurRFID
        {
            get => _curRFID;
            set=>SetProperty(ref _curRFID, value);
        }


        private RFIDParam _selectedItem;

        public RFIDParam SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }


        string writevalue, readvalue, connect, error;
        public string ConnectStatus
        {
            get => connect;
            set => SetProperty(ref connect, value);
        }

        public string Page { get; set; }


        public string WriteValue
        {
            get => writevalue;
            set => SetProperty(ref writevalue, value);
        }

        public string ReadValue
        {
            get => readvalue;
            set => SetProperty(ref readvalue, value);
        }

        public string Error
        {
            get => error;
            set => SetProperty(ref error, value);
        }
    }
}