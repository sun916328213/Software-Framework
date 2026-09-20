using Ctr_WPF_F48.Ctr_Halcon.View;
using DataManager.Log;
using Device.Camera;
using Device.Camera.Gige;
using Device.Camera.Gige.Definition;
using Device.Light;
using Extension.Core.Conversion;
using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using Extension.UI.Controls;
using HalconDotNet;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class CamManagerService
    {
        public CamManagerModel Model { get; set; } = new CamManagerModel();

        public readonly CamManager Manager;

        public CamManagerService(IContainerProvider container)
        {
            Manager = container.Resolve<CamManager>();

            ReadJson();
        }

        /// <summary>
        /// 连接相机
        /// </summary>
        public void Connect()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.CurCam.Connect();
            });
        }

        /// <summary>
        /// 设置当前相机参数
        /// </summary>
        public void SetCurCam()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.CurCam = Manager[Model.SelectedItem.Name];
                if (Model.CurCam == null)
                {
                    Manager.Create(Model.SelectedItem);
                    Model.CurCam= Manager[Model.SelectedItem.Name];
                    Model.ConnectStatus = LangSwitchEx.GetMessage(CamManagerMessage.未连接);
                    return;
                }
                Model.Exposure = Model.CurCam.Exposure;
                Model.Gain = Model.CurCam.Gain;
                Model.ConnectStatus = Model.CurCam.IsConnected == true ?
                    LangSwitchEx.GetMessage(CamManagerMessage.已连接) : LangSwitchEx.GetMessage(CamManagerMessage.未连接);
            });
        }

        /// <summary>
        /// 获取图像
        /// </summary>
        public void Grab()
        {
            TryCatchHelper.TryExecute(() =>
            {
                StopLive();
                Model.CurCam.Snap();
                var image = Model.CurCam.GetImage();
                Model.Image =  image.RawData.ToHImage(image.Width, image.Height);
            });
        }

        /// <summary>
        /// 设置相机参数 曝光 增益
        /// </summary>
        public void SetParam()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.CurCam.Exposure = Model.Exposure;
                Model.CurCam.Gain = Model.Gain;
            });
        }

        /// <summary>
        /// 实时控制
        /// </summary>
        public void Live()
        {
            TryCatchHelper.TryExecute(() =>
            {
                if (Model.CurCam.IsLive == false)
                    StartLive();
                else
                    StopLive();
            });
        }

        /// <summary>
        /// 开启实时
        /// </summary>
        private void StartLive()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.LiveButtonText = LangSwitchEx.GetMessage(CamManagerMessage.实时关闭);

                Action action = () =>
                {
                    var image = Model.CurCam.GetImage();
                    if(image.RawData.Length >0)
                    Model.Image = image.RawData.ToHImage (image.Width, image.Height);
                };
                Model.CurCam.StartLive(action);
            });
        }

        /// <summary>
        /// 停止实时
        /// </summary>
        private void StopLive()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.LiveButtonText = LangSwitchEx.GetMessage(CamManagerMessage.图像实时);
                Model.CurCam?.StopLive();
            });
        }

        /// <summary>
        /// 显示 实时十字线
        /// </summary>
        /// <param name="view"></param>
        public void ShowCross(HSmartWindowView view)
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.IsShowCross = !Model.IsShowCross;
                // 切换显示中心线的状态
                view.IsShowCross = Model.IsShowCross;
                if (Model.IsShowCross)
                    Model.ShowCrossButtonText = LangSwitchEx.GetMessage(CamManagerMessage.关闭中心十字线);
                else
                    Model.ShowCrossButtonText = LangSwitchEx.GetMessage(CamManagerMessage.显示中心十字线);
            });
        }

        private void ReadJson()
        { 
            // 读取新数据并更新现有集合，而不是替换引用
            var newData = JsonEx.Read<ObservableCollection<GigeCamParam>>(Model.FilePath, Model.FileName);
            if (newData != null)
                Model.DataCollection = newData;
        }

        /// <summary>
        /// 保存相机参数
        /// </summary>
        public void Save()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.CurCam.Exposure = Model.Exposure;
                Model.CurCam.Gain = Model.Gain;
                // 保存数据
                JsonEx.Save(Model.DataCollection, Model.FilePath, Model.FileName);

                ReadJson();
            });
        }

        /// <summary>
        /// 加载相机列表
        /// </summary>
        public async Task LoadAsync()
        {
            await TryCatchHelper.TryExecuteAsync(async () =>
             {
                 StopLive();
                 Manager.Disconnect();
                 Manager.Config(Model.DataCollection.ToList());
                 await Manager.ConnectAsync();
                 Model.CurCam = Manager[Model.SelectedItem.Name];
             });
        }
    }
}

