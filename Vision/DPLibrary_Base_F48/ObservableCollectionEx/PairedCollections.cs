using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;

namespace DPLibrary_Base_F48.ObservableCollectionEx
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

        private void CopyOneL2R(int index, string name) { var p = mPairs.FirstOrDefault(x => x.L.Name == name); if (p.L != null) SetR(index, p.L, p.R); }
        
        private void SetR(int index, PropertyInfo L, PropertyInfo R)
        {
            var oldV = R.GetValue(Right[index]);
            var newV = L.GetValue(Left[index]);
            if (!Equals(oldV, newV)) 
            { 
                R.SetValue(Right[index], newV); 
            }
        }

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

        private void Exit() 
        { 
            mSuspend--;
        }

        private bool IsSyncing => mSuspend > 1; // 正在对侧回调中

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

        public void Dispose()
        {
            Left.CollectionChanged -= OnLeftChanged;
            Right.CollectionChanged -= OnRightChanged;
        }
    }
}
