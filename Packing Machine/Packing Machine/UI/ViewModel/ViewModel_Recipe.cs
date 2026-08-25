using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extension.Core.ConvertEx;
using Extension.Core.EnumEx;
using Packing_Machine.Data.Model;
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
    internal class ViewModel_Recipe : ObservableObject
    {
        internal readonly Model_Recipe Model;
        internal readonly MenuService _MenuService;

        public ObservableCollection<CRecipe> Datas => Model.Recipes;


        public string[] FOSBType => File.ReadAllText("Config//FOSBName.json").FromJson<string[]>();

        public string[] BagType => File.ReadAllText("Config//BagType.json").FromJson<string[]>();

        public string[] LabelDir => EnumEx.GetName<ELabelDir>();


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
        private IEnumerable<ELabelDir> curRecipeLabelDir_First;
        public IEnumerable<ELabelDir> CurRecipeLabelDir_First
        {

            get => curRecipeLabelDir_First;
            set => SetProperty(ref curRecipeLabelDir_First, value);
        }


        private IEnumerable<int> curRecipeLabelNum_First;

        public IEnumerable<int> CurRecipeLabelNum_First
        {
            get => curRecipeLabelNum_First;
            set => SetProperty(ref curRecipeLabelNum_First, value);
        }
        #endregion
        #region 第二层
        private IEnumerable<ELabelDir> curRecipeLabelDir_Second;
        public IEnumerable<ELabelDir> CurRecipeLabelDir_Second
        {

            get => curRecipeLabelDir_Second;
            set => SetProperty(ref curRecipeLabelDir_Second, value);
        }


        private IEnumerable<int> curRecipeLabelNum_Second;

        public IEnumerable<int> CurRecipeLabelNum_Second
        {
            get => curRecipeLabelNum_Second;
            set => SetProperty(ref curRecipeLabelNum_Second, value);
        }
        #endregion


        public RelayCommand AddCommand { get; }

        public RelayCommand ModifyCommand { get; }

        public RelayCommand SaveCommand { get; }

        public RelayCommand SettingCommand { get; }

        public RelayCommand UpdateCommand { get; }
        public RelayCommand AddLabelCommand { get; }

        public RelayCommand<string> LabelManger_Add { get; }


        public ViewModel_Recipe( IContainerProvider  container)
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
                            CurRecipeLabelNum_First = Model.GetCurRecipeLabelNum(CurLabelTimes);
                            break;
                        case 2:
                            CurRecipeLabelDir_Second = Model.GetCurRecipeLabelDir(CurLabelTimes);
                            CurRecipeLabelNum_Second = Model.GetCurRecipeLabelNum(CurLabelTimes);
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
                LabelInfo_AddLabel.LabelNum = 1;
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
                    CurRecipeLabelNum_First = Model.GetCurRecipeLabelNum(1);

                    CurRecipeLabelDir_Second = Model.GetCurRecipeLabelDir(2);
                    CurRecipeLabelNum_Second = Model.GetCurRecipeLabelNum(2);

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
                _MenuService.Model.SelectNavigationItems = _MenuService.Query("打印设置");
                _MenuService.ExecuteNavigationItem();
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
