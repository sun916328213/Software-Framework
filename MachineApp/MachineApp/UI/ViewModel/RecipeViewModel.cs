// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：RecipeViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Extension.Core.Conversion、MachineApp.UI.Model、MachineApp.UI.WindowUI、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extension.Core.Conversion;
using MachineApp.UI.Model;
using MachineApp.UI.WindowUI;
using Prism.Ioc;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using WPFCustomControls.Window.Views;

namespace MachineApp.UI.ViewModel
{
    /// <summary>
    /// RecipeViewModel 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal class RecipeViewModel : ObservableObject
    {
        internal readonly Model_Recipe Model;
        internal readonly MenuService _MenuService;

        public ObservableCollection<CRecipe> Datas => Model.Recipes;


        public string[] FOSBType => File.ReadAllText("Config//ProcessInfo//FOSBName.json").FromJson<string[]>();

        public string[] BagType => File.ReadAllText("Config//ProcessInfo//BagType.json").FromJson<string[]>();

        // 位置名称 + PLC 编号都在 LabelPosition.json，读取统一走 CLabelInfo_Single：
        // 带缓存（不像上面两个每次读盘），配置坏了回退空表而不是直接抛。
        public string[] LabelDir => CLabelInfo_Single.LabelDirs;


        private CLabelInfo_Single labelInfo_AddLabel = new CLabelInfo_Single();
        /// <summary>
        /// 在AddLabelInfo界面的标签信息
        /// </summary>
        public CLabelInfo_Single LabelInfo_AddLabel
        {
            get => labelInfo_AddLabel;
            set => SetProperty(ref labelInfo_AddLabel, value);
        }


        private ObservableCollection<CLabelInfo_Single> labelInfoList;
        /// <summary>
        ///  标签信息集合
        /// </summary>
        public ObservableCollection<CLabelInfo_Single> LabelInfoList
        {
            get => labelInfoList;
            set => SetProperty(ref labelInfoList, value);
        }

        private CRecipe curRecipe = new CRecipe();
        /// <summary>
        /// 当前的Recipe中选中的/新建信息
        /// </summary>
        public CRecipe CurRecipe
        {
            get => curRecipe;
            set
            {
                if (SetProperty(ref curRecipe, value))
                    Model.CurRecipe = CurRecipe;
            }
        }

        public int SelectIndex { get; set; } = -1;

        /// <summary>
        /// 贴标层数ID
        /// </summary>
        public int CurLabelTimes { get; set; }

        #region 第一层
        private IEnumerable<string> curRecipeLabelDir_First;

        /// <summary>
        /// 第一次贴标的标签方向集合 FOSB本体
        /// </summary>
        public IEnumerable<string> CurRecipeLabelDir_First
        {

            get => curRecipeLabelDir_First;
            set => SetProperty(ref curRecipeLabelDir_First, value);
        }
 
        #endregion
        #region 第二层
        private IEnumerable<string> curRecipeLabelDir_Second;

        /// <summary>
        /// 第二次贴标的标签方向集合 AL袋
        /// </summary>
        public IEnumerable<string> CurRecipeLabelDir_Second
        {

            get => curRecipeLabelDir_Second;
            set => SetProperty(ref curRecipeLabelDir_Second, value);
        }
 
        #endregion


        public RelayCommand AddCommand { get; }

        public RelayCommand ModifyCommand { get; }

        public RelayCommand SaveCommand { get; }

        public RelayCommand SettingCommand { get; }

        public RelayCommand UpdateCommand { get; }
        public RelayCommand AddLabelCommand { get; }

        public RelayCommand<string> LabelManger_Add { get; }


        /// <summary>
        /// 初始化 RecipeViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve、GetCurRecipeLabelDir、Addlabel、ToInt32、ShowDialog；具体条件和先后次序见方法体。
        /// </summary>
        public RecipeViewModel( IContainerProvider  container)
        {
            Model = container.Resolve<Model_Recipe>();

            _MenuService = container.Resolve<MenuService>(); ;

            Model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "CurRecipe")
                {
                    switch (CurLabelTimes)
                    {
                        case 1:
                            CurRecipeLabelDir_First = Model.GetCurRecipeLabelDir(CurLabelTimes);
                        
                            break;
                        case 2:
                            CurRecipeLabelDir_Second = Model.GetCurRecipeLabelDir(CurLabelTimes);
                     
                            break;
                    }


                }
            };

            AddLabelCommand = new RelayCommand(() =>
            {

                Model.Addlabel(CurLabelTimes, labelInfo_AddLabel);
            });

            LabelManger_Add = new RelayCommand<string>((index) =>
            {
                CurLabelTimes = Convert.ToInt32(index);
                AddLabelInfo addLabelInfo = new AddLabelInfo();
                addLabelInfo.DataContext = this;
                 
                addLabelInfo.ShowDialog();
            });

            AddCommand = new RelayCommand(() =>
               {
                   CurRecipe = new CRecipe() { ID = (Datas.Count + 1).ToString() };

                   RecipeSetting recipeSetting;
                   recipeSetting = new RecipeSetting();
                   recipeSetting.DataContext = this;
                   if ((bool)recipeSetting.ShowDialog())
                   {
                     Model.Add(CurRecipe);
                   }
                   recipeSetting.Close();
               });

            ModifyCommand = new RelayCommand(() =>
            {
                if (CurRecipe != null && CurRecipe.ID != null)
                {
                    CurRecipeLabelDir_First = Model.GetCurRecipeLabelDir(1);
                
                    CurRecipeLabelDir_Second = Model.GetCurRecipeLabelDir(2);
               
                    RecipeSetting recipeSetting;
                    recipeSetting = new RecipeSetting();
                    recipeSetting.DataContext = this;
                    if ((bool)recipeSetting.ShowDialog())
                    {
                        Model.Modify(SelectIndex, CurRecipe);
                    }
                    recipeSetting.Close();
                }
            });

            SaveCommand = new RelayCommand(() =>
            {
                Model.Save();
            });

            SettingCommand = new RelayCommand(() => {
               MessagePrompt message = new  MessagePrompt("提示", "选择配方后请再去打印界面设置标签数据,即将转去打印界面!!", System.Windows.Visibility.Hidden);
                message.ShowDialog ();
               _MenuService.SwitchMenu("打印设置");
            });

            UpdateCommand = new RelayCommand(() =>
            {
                //从PLC读取数据进行更新
                Model.Save();
                //更新后存储
            });
        }
    }
}

