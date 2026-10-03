// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：CLabelInfo_Single、CLabelInfoCollection、CRecipe、Model_Recipe。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、Extension.Core.Conversion、Extension.Core.Buffers、Extension.IO.Compression、Extension.Integration.Cloning、ImTools。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Core.Conversion;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using ImTools;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace MachineApp.UI.Model
{
    /// <summary>
    /// CLabelInfo_Single 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public class CLabelInfo_Single
    {
        public static List<CLabelInfo_Single> LabelDirConfig = JsonEx.Read<List<CLabelInfo_Single>>("Config//ProcessInfo//LabelPosition.json");

        public static  string[] LabelDirs = LabelDirConfig.Select(x => x.LabelDir).ToArray();   



        private string labelDir;

        /// <summary>
        /// 标签具体位置（LabelPosition.json 里的名称）
        /// </summary>
        public string LabelDir
        {
            get => labelDir;
            set
            {
                labelDir = value;
                // 位置一变，发给 PLC 的编号同步跟着变，不需要手工维护
                ID = LabelDirConfig.First(x => x.LabelDir == labelDir).ID;
            }
        }

        /// <summary>
        /// 标签位置ID 主要用于发送给PLC
        /// 由 <see cref="LabelDir"/> 自动带出，不要手工赋值。
        /// 注意：若配方存档里也写了 ID，反序列化时会按存档值覆盖（System.Text.Json 按
        /// json 里的先后顺序赋值）。想让它永远以配置表为准，给这个属性加 [JsonIgnore]。
        /// </summary>
        public int ID { get; set; }

    }

    /// <summary>
    /// CLabelInfoCollection 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
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

    /// <summary>
    /// CRecipe 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
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

    /// <summary>
    /// Model_Recipe 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal class Model_Recipe :ObservableObject
    {

        /// <summary>
        /// 初始化 Model_Recipe 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Load；具体条件和先后次序见方法体。
        /// </summary>
        public Model_Recipe()
        {
            Load();
        }
        /// <summary>
        /// 标签位置和ID映射
        /// </summary>
        public  List<CLabelInfo_Single> LabelPositionConfig =JsonEx.Read<List<CLabelInfo_Single>>("Config//ProcessInfo//LabelPosition.json");

        private string Path = "Config//ProcessInfo//Recipe";
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
            set => SetProperty(ref curRecipe, value);
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
                MessageBox.Show("标签方向已经存在,请检查后重新添加！！");
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
        public IEnumerable<string> GetCurRecipeLabelDir(int pTimes)
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
        /// 添加
        /// </summary>
        /// <param name="pRecipe"></param>
        /// <returns></returns>
        public void Add(CRecipe pRecipe)
        {
            var data = Recipes.FindFirst(s => s.ID == pRecipe.ID);
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

        /// <summary>
        /// 执行加载操作；对应入口为 Load。
        /// 正文中的关键协作调用包括 ReadAllText、FromJson；具体条件和先后次序见方法体。
        /// </summary>
        public void Load()
        {
            string json = File.ReadAllText(Path);
            Recipes = json.FromJson<ObservableCollection<CRecipe>>();

        }
    }
}

