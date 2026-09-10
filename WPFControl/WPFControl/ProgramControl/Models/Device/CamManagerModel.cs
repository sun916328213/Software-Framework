using Device.Camera;
using Device.Camera.Gige;
using Device.Camera.Gige.Definition;
using Extension.Core.SystemEx;
using HalconDotNet;
using WPFControl.CustomControl.Models;
using WPFCustomControls.Controls;

namespace WPFControl.ProgramControl.Models
{
    public static class CamManagerMessage
    {
        public static readonly string[] 已连接 = { "已连接", "Connected" };
        public static readonly string[] 未连接 = { "未连接", "Not Connected" };
        public static readonly string[] 相机 = { "相机", "Camera" };
        public static readonly string[] 显示中心十字线 = { "显示中心十字线", "Display Central Cross" };
        public static readonly string[] 关闭中心十字线 = { "关闭中心十字线", "Close Central Cross" };
        public static readonly string[] 图像实时 = { "图像实时", "Live Star" };
        public static readonly string[] 实时关闭 = { "实时关闭", "Live Stop" };
    }

    public class CamManagerModel : ConfigManagerModel<GigeCamParam>
    {
        public CamManagerModel()
        {
            Name = LangSwitchEx.GetMessage(CamManagerMessage.相机);
            FilePath = "Config//";
            FileName = "CamParam.json";
        }

        private GigeCamParam _selectedItem =new GigeCamParam();

        public GigeCamParam SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        ICamera curcam;
        public ICamera CurCam
        {
            get => curcam;
            set=>SetProperty(ref curcam, value);
        }


        string connect,   fps;
        HObject image;
        public HObject Image
        {
            get => image;
            set => SetProperty(ref image, value);
        }
        public string ConnectStatus
        {
            get => connect;
            set => SetProperty(ref connect, value);
        }
 
        public string FPS
        {
            get => fps;
            set => SetProperty(ref fps, value);
        }


        // 添加Live按钮文字属性
        internal string _liveButtonText = LangSwitchEx.GetMessage(CamManagerMessage.图像实时);  
        public string LiveButtonText
        {
            get => _liveButtonText;
            set => SetProperty(ref _liveButtonText, value);
        }

        // 添加Live按钮文字属性
        internal string _showCrossButtonText = LangSwitchEx.GetMessage(CamManagerMessage.显示中心十字线);
        public string ShowCrossButtonText
        {
            get => _showCrossButtonText;
            set => SetProperty(ref _showCrossButtonText, value);
        }
 
        
        public int Exposure
        {
            get => _selectedItem.Exposure;
            set
            {
                _selectedItem.Exposure=value;
                OnPropertyChanged(nameof(Exposure));
            }
        }

      
        public double Gain
        {
            get => _selectedItem.Gain;
            set {
                _selectedItem.Gain = value;
                OnPropertyChanged(nameof(Gain));
            }
        }

        bool ishowcross;
        public bool IsShowCross
        {
            get => ishowcross;
            set => SetProperty(ref ishowcross, value);
        }

    }
}