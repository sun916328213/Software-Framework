using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extension.Core.ConvertEx;
using Packing_Machine.UI.Model;
using Packing_Machine.UI.WindowUI;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;
using WPFCustomControls.Window.Views;

namespace Packing_Machine.UI.ViewModel
{
    internal class RecipeViewModel : ObservableObject
    {
        internal readonly Model_Recipe Model;
        internal readonly MenuService _MenuService;

        public ObservableCollection<CRecipe> Datas => Model.Recipes;


        public string[] FOSBType => File.ReadAllText("Config//ProcessInfo//FOSBName.json").FromJson<string[]>();

        public string[] BagType => File.ReadAllText("Config//ProcessInfo//BagType.json").FromJson<string[]>();

        public string[] LabelDir => File.ReadAllText("Config//ProcessInfo//LabelPosition.json").FromJson<string[]>();


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

