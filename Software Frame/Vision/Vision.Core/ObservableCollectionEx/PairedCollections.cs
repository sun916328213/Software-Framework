// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：PairedCollections。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System.Collections.ObjectModel、System.Collections.Specialized、System.ComponentModel、System.Reflection。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;

namespace Vision.Core.ObservableCollectionEx
{
    /// <summary>
    /// 集合同步器
    /// </summary>
    /// <typeparam name="TL"></typeparam>
    /// <typeparam name="TR"></typeparam>
    public sealed class PairedCollections<TL, TR> : IDisposable
    {
        public ObservableCollection<TL> Left { get; }
        public ObservableCollection<TR> Right { get; }

        private readonly Func<TL, TR> mMakeRightFromLeft;
        private readonly Func<TR, TL> mMakeLeftFromRight;

        private readonly List<(PropertyInfo L, PropertyInfo R)> mPairs = new List<(PropertyInfo L, PropertyInfo R)>();

        // 防递归同步
        private int mSuspend;

        /// <summary>
        /// 初始化 PairedCollections 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof、GetProperty、Add、CopyOneL2R、IndexOf；具体条件和先后次序见方法体。
        /// </summary>
        public PairedCollections(
            ObservableCollection<TL> left,
            ObservableCollection<TR> right,
            Func<TL, TR> makeRightFromLeft,
            Func<TR, TL> makeLeftFromRight,
           string[] leftPropNames, string[] rightPropNames)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
            mMakeRightFromLeft = makeRightFromLeft ?? throw new ArgumentNullException(nameof(makeRightFromLeft));
            mMakeLeftFromRight = makeLeftFromRight ?? throw new ArgumentNullException(nameof(makeLeftFromRight));

            if (leftPropNames.Length == rightPropNames.Length && leftPropNames.Length > 0)
            {
                for (int i = 0; i < leftPropNames.Length; i++)
                {
                    PropertyInfo lpi = typeof(TL).GetProperty(leftPropNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    PropertyInfo rpi = typeof(TR).GetProperty(rightPropNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    mPairs.Add((lpi, rpi));
                }
            }
            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] is INotifyPropertyChanged nl)
                {
                    nl.PropertyChanged += (s2, e2) =>
                    {
                        TL inst = (TL)s2;
                        CopyOneL2R(Left.IndexOf(inst), e2.PropertyName);
                    };
                }
            }

            Left.CollectionChanged += OnLeftChanged;
            Right.CollectionChanged += OnRightChanged;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CopyOneL2R。
        /// 正文中的关键协作调用包括 FirstOrDefault、SetR；具体条件和先后次序见方法体。
        /// </summary>
        private void CopyOneL2R(int index, string name) { var p = mPairs.FirstOrDefault(x => x.L.Name == name); if (p.L != null) SetR(index, p.L, p.R); }
        
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetR。
        /// 正文中的关键协作调用包括 GetValue、Equals、SetValue；具体条件和先后次序见方法体。
        /// </summary>
        private void SetR(int index, PropertyInfo L, PropertyInfo R)
        {
            var oldV = R.GetValue(Right[index]);
            var newV = L.GetValue(Left[index]);
            if (!Equals(oldV, newV)) 
            { 
                R.SetValue(Right[index], newV); 
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Enter。
        /// </summary>
        private bool Enter() 
        {
            if (mSuspend == 0)
            {
                mSuspend++;
                return true; // 允许进入
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Exit。
        /// </summary>
        private void Exit() 
        { 
            mSuspend--;
        }

        private bool IsSyncing => mSuspend > 1; // 正在对侧回调中

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnLeftChanged。
        /// 正文中的关键协作调用包括 Enter、mMakeRightFromLeft、Insert、CopyOneL2R、IndexOf；具体条件和先后次序见方法体。
        /// </summary>
        private void OnLeftChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Enter()) return;
            try
            {
                if (IsSyncing) return; // 避免循环

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        for (int i = 0; i < e.NewItems.Count; i++)
                        {
                            var newLeft = (TL)e.NewItems[i];
                            var newRight = mMakeRightFromLeft(newLeft);
                            int idx = e.NewStartingIndex + i;
                            Right.Insert(idx >= 0 ? idx : Right.Count, newRight);

                            if (newLeft is INotifyPropertyChanged nl)
                            {
                                nl.PropertyChanged += (s2, e2) =>
                                {
                                    TL inst = (TL)s2;
                                    CopyOneL2R(Left.IndexOf(inst), e2.PropertyName);
                                };
                            }

                        }
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        for (int i = 0; i < e.OldItems.Count; i++)
                        {
                            int idx = e.OldStartingIndex; // 每次删除后后续元素左移，但同一个起点删除 N 次即可
                            if (e.OldItems[i] is INotifyPropertyChanged nl)
                            {
                                nl.PropertyChanged -= (s2, e2) =>
                                {
                                    TL inst = (TL)s2;
                                    CopyOneL2R(Left.IndexOf(inst), e2.PropertyName);
                                };
                            }
                            
                            if (idx >= 0 && idx < Right.Count) 
                            {
                                Right.RemoveAt(idx); 
                            }
                        }
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        for (int i = 0; i < e.NewItems.Count; i++)
                        {
                            var newLeft = (TL)e.NewItems[i];
                            var newRight = mMakeRightFromLeft(newLeft);
                            int idx = e.NewStartingIndex + i;
                            Right[idx] = newRight;
                        }
                        break;
                    case NotifyCollectionChangedAction.Move:
                        // 移动
                        Right.Move(e.OldStartingIndex, e.NewStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        // 统一用左边重建右边
                        Right.Clear();
                        foreach (var l in Left)
                            Right.Add(mMakeRightFromLeft(l));
                        break;
                }
            }
            finally 
            { 
                Exit(); 
            }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnRightChanged。
        /// 正文中的关键协作调用包括 Enter、mMakeLeftFromRight、Insert、RemoveAt、Move；具体条件和先后次序见方法体。
        /// </summary>
        private void OnRightChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Enter()) return;
            try
            {
                if (IsSyncing) return;

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        for (int i = 0; i < e.NewItems.Count; i++)
                        {
                            var newRight = (TR)e.NewItems[i];
                            var newLeft = mMakeLeftFromRight(newRight);
                            int idx = e.NewStartingIndex + i;
                            Left.Insert(idx >= 0 ? idx : Left.Count, newLeft);
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        for (int i = 0; i < e.OldItems.Count; i++)
                        {
                            int idx = e.OldStartingIndex;
                            if (idx >= 0 && idx < Left.Count)
                            { 
                                Left.RemoveAt(idx);
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        for (int i = 0; i < e.NewItems.Count; i++)
                        {
                            var newRight = (TR)e.NewItems[i];
                            var newLeft = mMakeLeftFromRight(newRight);
                            int idx = e.NewStartingIndex + i;
                            Left[idx] = newLeft;
                        }
                        break;
                    case NotifyCollectionChangedAction.Move:
                        Left.Move(e.OldStartingIndex, e.NewStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        Left.Clear();
                        foreach (var r in Right)
                            Left.Add(mMakeLeftFromRight(r));
                        break;
                }
            }
            finally 
            { 
                Exit();
            }
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// </summary>
        public void Dispose()
        {
            Left.CollectionChanged -= OnLeftChanged;
            Right.CollectionChanged -= OnRightChanged;
        }
    }
}
