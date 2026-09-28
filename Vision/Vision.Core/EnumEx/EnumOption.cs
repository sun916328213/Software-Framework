using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
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

        public EnumOption(T value, ICollection<T> selected)
        {
            Value = value;
            mSelected = selected;
            mIsChecked = selected.Contains(value); // 初始化选中态
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
