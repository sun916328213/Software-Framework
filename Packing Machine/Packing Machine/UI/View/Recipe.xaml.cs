using DataManager.SQL;
using DataManager.SQL.Definition;
using Packing_Machine.Business.Flow;
using Packing_Machine.Business.Order;
using Packing_Machine.Business.Station;
using Packing_Machine.Data.Model;
using Packing_Machine.Infra.Tool;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Packing_Machine.UI.View
{
    /// <summary>
    /// Recipe.xaml 的交互逻辑
    /// </summary>
    public partial class Recipe : UserControl
    {
        AutoProcess autoProcess;
        PLCTool pLCTool;
       
        public Recipe()
        {
            InitializeComponent();
            var app = (App)Application.Current;
            var container = app.Container;
              autoProcess = container.Resolve<AutoProcess>();

            pLCTool= container.Resolve<PLCTool>();
        }

        private void Button_Click1(object sender, RoutedEventArgs e)
        {
            autoProcess._Load.Trigger(ELoad.读取RFID, PLCSensor.Load_Trigger);
        }

        private void Button_Click2(object sender, RoutedEventArgs e)
        {
            autoProcess._ReadID.Trigger(EReadID.读取FOSBID, PLCSensor.Load_Trigger);
        }

        private void Button_Click3(object sender, RoutedEventArgs e)
        {
            autoProcess._HumidityCard.Trigger(EHumidityCard.九点标定开始, PLCSensor.RobotCenterCam_Trigger);
        }

        private void Button_Click4(object sender, RoutedEventArgs e)
        {
            autoProcess._HumidityCard.Trigger(EHumidityCard.湿度卡相机准基, PLCSensor.RobotCenterCam_Trigger);
        }

        private void Button_Click5(object sender, RoutedEventArgs e)
        {
            autoProcess._HumidityCard.Trigger(EHumidityCard.颜色传感器位置示教, PLCSensor.RobotCenterCam_Trigger);
        }

        private void Button_Click6(object sender, RoutedEventArgs e)
        {
            autoProcess._HumidityCard.Trigger(EHumidityCard.湿度卡相机检测, PLCSensor.RobotCenterCam_Trigger);
        }

        private async void Button_Click7(object sender, RoutedEventArgs e)
        {
            await autoProcess._Label.Trigger(EProcess.贴标流程,ELabel.FOSB贴前检测, PLCSensor.RobotCenterCam_Trigger);
        }

        private void Button_Click8(object sender, RoutedEventArgs e)
        {
            autoProcess._Label.Trigger(EProcess.贴标流程, ELabel.FOSB贴后检测, PLCSensor.RobotCenterCam_Trigger);
        }
        private async void Button_Click72(object sender, RoutedEventArgs e)
        {
            await autoProcess._Label.Trigger(EProcess.贴标流程, ELabel.AL贴前检测, PLCSensor.RobotCenterCam_Trigger);
        }

        private void Button_Click82(object sender, RoutedEventArgs e)
        {
            autoProcess._Label.Trigger(EProcess.贴标流程, ELabel.AL贴后检测, PLCSensor.RobotCenterCam_Trigger);
        }

        private void Button_Click9(object sender, RoutedEventArgs e)
        {
            autoProcess._ReadID.Trigger(EReadID.进站存档, PLCSensor.RobotSideCam_Trigger);
        }

        private void Button_Click10(object sender, RoutedEventArgs e)
        {
            autoProcess._UnLoad.Trigger(PLCSensor. RobotSideCam_Trigger);
        }

        private void Button_Click11(object sender, RoutedEventArgs e)
        {
            autoProcess._Label.Trigger(EProcess.贴标流程, ELabel.打印标签, PLCSensor.Print_Trigger);
        }

        private void Button_Click12(object sender, RoutedEventArgs e)
        {
            autoProcess._Accessory.Trigger(PLCSensor.Accessory_Trigger);
        }

        private void Button_Click13(object sender, RoutedEventArgs e)
        {
            autoProcess._HeatSeal.Trigger(PLCSensor.Heat_Trigger);
        }

        private void Button_Click14(object sender, RoutedEventArgs e)
        {
            autoProcess._ALDetect.Trigger(EALDetect.AL示教, PLCSensor.AL_Trigger);
        }

        private void Button_Click15(object sender, RoutedEventArgs e)
        {
            autoProcess._ALDetect.Trigger(EALDetect.AL检测, PLCSensor.AL_Trigger);
        }

        private void Button_Click16(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.LoadProcess_Trigger, EProcess.进站流程);
        }

        private void Button_Click17(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.AccessoryProcess_Trigger, EProcess.配件流程);
        }

        private void Button_Click18(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.FOSBLabelProcess_Trigger, EProcess.贴标流程);
        }

        private void Button_Click19(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.PackagingProcess_Trigger, EProcess.入袋流程);
        }

        private void Button_Click20(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.EVAProcess_Trigger, EProcess.抽真空流程);
        }

        private void Button_Click21(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.NitProcess_Trigger, EProcess.冲氮气流程);
        }

        private void Button_Click22(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.HeatProcess_Trigger, EProcess.热封流程);
        }

        private void Button_Click23(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.ALLabelProcess_Trigger, EProcess.贴标流程);
        }

        private void Button_Click24(object sender, RoutedEventArgs e)
        {
            autoProcess._Process.ProcessTrigger(PLCProcess.UnloadProcess_Trigger, EProcess.出站流程);
        }

        private void Button_Click25(object sender, RoutedEventArgs e)
        {
            pLCTool.CompleteAll(PLCSystem.PC_SysTrigger, PLCSystem.PC_SysTrigger, 1000);
        }

        private async void Button_Click51(object sender, RoutedEventArgs e)
        {
           await pLCTool.TriggerAsync(PLCSensor.RecipeSelect_Trigger, PLCSensor.RecipeSelect_Trigger, "10");
        }
        private async void Button_Click26(object sender, RoutedEventArgs e)
        {
            try
            {
                IContainerProvider container = ContainerLocator.Container;


                var _SQLManager = container.Resolve<SQLManager>();

                //连接数据库
                _SQLManager[0].Connect();
            }
            catch (Exception ex)
            {
            }
        }
    }
}