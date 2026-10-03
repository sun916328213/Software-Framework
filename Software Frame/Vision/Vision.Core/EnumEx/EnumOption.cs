// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：EnumOption。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.ComponentModel、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
    /// <summary>
    /// EnumOption 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：INotifyPropertyChanged。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class EnumOption<T> : INotifyPropertyChanged
    {
        private bool mIsChecked;
        private readonly ICollection<T> mSelected;

        public T Value { get; }
        public bool IsChecked
        {
            get => mIsChecked;
            set
            {
                if (mIsChecked == value) return;
                mIsChecked = value;
                if (value)
                {
                    if (!mSelected.Contains(Value)) mSelected.Add(Value);
                }
                else
                {
                    if (mSelected.Contains(Value)) mSelected.Remove(Value);
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        /// <summary>
        /// 初始化 EnumOption 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Contains；具体条件和先后次序见方法体。
        /// </summary>
        public EnumOption(T value, ICollection<T> selected)
        {
            Value = value;
            mSelected = selected;
            mIsChecked = selected.Contains(value); // 初始化选中态
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
