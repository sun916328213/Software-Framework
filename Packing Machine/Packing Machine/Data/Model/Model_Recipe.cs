using Extension.Core.ConvertEx;
using Extension.Core.DataEx;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using WPFControl.ProgramControl.Models;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Packing_Machine.Data.Model
{
    public class CLabelInfo_Single
    {
        /// <summary>
        /// 标签方向
        /// </summary>
        public ELabelDir LabelDir { get; set; }= ELabelDir.Fornt;
        /// <summary>
        /// 标签数量
        /// </summary>
        public int LabelNum { get; set; } = 1;
    }

    public class CLabelInfoCollection
    {
        /// <summary>
        /// 第一次标信息集合
        /// </summary>
        public List<CLabelInfo_Single> LabelInfo_First { get; set; }=new List<CLabelInfo_Single>();
        /// <summary>
        /// 第二次标信息集合
        /// </summary>
        public List<CLabelInfo_Single> LabelInfo_Second { get; set; } = new List<CLabelInfo_Single>();
        /// <summary>
        /// 第三次标信息集合
        /// </summary>
        public List<CLabelInfo_Single> LabelInfo_Three { get; set; } = new List<CLabelInfo_Single>();
    }

    public class CRecipe
    {
        /// <summary>
        /// 配方ID
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// FOSB盒子名称
        /// </summary>
        public string FOSBType { get; set; }
        /// <summary>
        /// 标签信息集合  
        /// </summary>
        public CLabelInfoCollection LabelInfoCollection { get; set; } = new CLabelInfoCollection();
       
        /// <summary>
        /// 干燥剂数量
        /// </summary>
        public int Num_Desiccant { get; set; } = 4;
        /// <summary>
        /// 湿度卡数量
        /// </summary>
        public int Num_Humidity { get; set; } = 1;
 
    }

    internal class Model_Recipe :ObservableObject
    {

        public Model_Recipe()
        {
            Load();
        }

        private string Path = "Config//Recipe";
        /// <summary>
        /// 配方集合
        /// </summary>
       internal  ObservableCollection<CRecipe> Recipes { get; set; }=new ObservableCollection<CRecipe>();
 
        private CRecipe curRecipe = new CRecipe();
        /// <summary>
        /// 当前配方
        /// </summary>
        internal CRecipe CurRecipe
        {
            get => curRecipe;
            set
            {
                curRecipe = value;
                OnPropertyChanged("CurRecipe");
            }
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        /// <param name="pTimes">贴标层数</param>
        /// <param name="pLabelInfo"></param>
        /// <returns></returns>
        public void Addlabel(int pTimes, CLabelInfo_Single pLabelInfo)
        {

            CLabelInfo_Single labelInfo = pLabelInfo.DeepClone();

            List<CLabelInfo_Single> curdata = GetCurTimesLabelInfo(pTimes);

            //判断贴标的方向是否存在
            var data1 = curdata.FirstOrDefault(s => s.LabelDir == labelInfo.LabelDir);
            if (data1 != null)
            {
                data1.LabelDir = labelInfo.LabelDir;
                data1.LabelNum = labelInfo.LabelNum;
            }
            else
                curdata.Add(labelInfo);

            CurRecipe = CurRecipe;
        }

        /// <summary>
        /// 获取当前层数的标签集合
        /// </summary>
        /// <param name="pTimes"></param>
        /// <returns></returns>
        private List<CLabelInfo_Single> GetCurTimesLabelInfo(int pTimes)
        {
            List < CLabelInfo_Single > curdata=new List<CLabelInfo_Single>();
            switch (pTimes)
            {
                case 1:
                    curdata = CurRecipe.LabelInfoCollection.LabelInfo_First; break;
                case 2:
                    curdata = CurRecipe.LabelInfoCollection.LabelInfo_Second; break;
                case 3:
                    curdata = CurRecipe.LabelInfoCollection.LabelInfo_Three; break;
            }
            return curdata;
        }

        /// <summary>
        /// 获取当前配方的标签方向
        /// </summary>
        /// <param name="pTimes"></param>
        /// <returns></returns>
        public IEnumerable<ELabelDir> GetCurRecipeLabelDir(int pTimes)
        {
            try
            {
                return GetCurTimesLabelInfo(pTimes).Select(s => s.LabelDir);
            }
            catch
            { 
              return null;
            }
        }

        /// <summary>
        /// 获取当前配方的标签数量
        /// </summary>
        /// <param name="pTimes"></param>
        /// <returns></returns>
        public IEnumerable<int> GetCurRecipeLabelNum(int pTimes)
        {
            try
            {
                return GetCurTimesLabelInfo(pTimes).Select(s => s.LabelNum);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="pRecipe"></param>
        /// <returns></returns>
        public void Add(CRecipe pRecipe)
        {
            var data = Recipes.Where(s => s.ID == pRecipe.ID);
            if (data != null) throw new Exception("ID already exists!!");

            Recipes.Add(pRecipe);
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="pIndex"></param>
        /// <param name="pNew"></param>
        /// <returns></returns>
        public void Modify(int pIndex, CRecipe pNew)
        {
                Recipes[pIndex] = pNew;
        }

        /// <summary>
        /// 保存到本地
        /// </summary>
        /// <returns></returns>
        public void Save()
        {
                string json = Recipes.ToJson();
                File.WriteAllText(Path, json);
        }

        public void Load()
        {
               //string json= File.ReadAllText(Path  );
                //Recipes = json.FromJson<ObservableCollection<CRecipe>>();

        }
    }
}
