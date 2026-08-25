using Extension.Core.ConvertEx;
using System;

namespace Communication 
{
    public class CRegisterInfo
    {
        /// <summary>
        /// 数据名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 数据点位地址
        /// </summary>
        public string Address { get;  set; }
        /// <summary>
        /// 数据类型
        /// </summary>
        public EDataType Type { get; set; }
        /// <summary>
        /// 数据长度
        /// </summary>
        public int Length { get; set; }
    }

    /// <summary>
    /// 寄存器信息结构体
    /// </summary>
    public class CRegisterManager
    {
        /// <summary>
        /// 寄存器信息
        /// </summary>
        public CRegisterInfo RegisterInfo { get; set; }
        /// <summary>
        /// 寄存器地址去字母
        /// </summary>
        public int Address { get; set; }
        /// <summary>
        /// 数据值
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 是否已经被触发
        /// </summary>
        public bool IsTrigger { get; set; } = false;
        /// <summary>
        /// 任务名称
        /// </summary>
        public string TaskName { get; set; }



        /// <summary>
        /// 用于存储事件处理程序的委托字段
        /// </summary>
        private EventHandler<CRegisterManager> _valueChanged;

        /// <summary>
        /// 线程同步锁
        /// </summary>
        private readonly object _eventLock = new object();

        /// <summary>
        /// 数据变更事件，当数据发生变化时触发
        /// </summary>
        public event EventHandler<CRegisterManager> DataChanged
        {
            add
            {
                lock (_eventLock)
                {
                    // 使用空委托模式避免null检查
                    _valueChanged = (EventHandler<CRegisterManager>)Delegate.Combine(_valueChanged, value);
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _valueChanged = (EventHandler<CRegisterManager>)Delegate.Remove(_valueChanged, value);
                }
            }
        }

        /// <summary>
        /// 触发数据变更事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnDataChanged(CRegisterManager e)
        {
            // 复制委托以避免线程安全问题
            EventHandler<CRegisterManager> handler = _valueChanged;

            // 触发事件
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// 触发回调
        /// </summary>
        /// <param name="value"></param>
        public void TriggerCallBack(CRegisterManager value)
        {
            if (_valueChanged != null)
            {
                _valueChanged.BeginInvoke(this, value, null, null);//需要异步执行，否则回卡线程
            }
        }

    

        public CRegisterManager(CRegisterInfo pRegisterInfo)
        {
            RegisterInfo = pRegisterInfo;
            switch (RegisterInfo.Type)
            {
                case EDataType.Bit:
                    RegisterInfo.Length = 1; break;
                case EDataType.Short:
                    RegisterInfo.Length = 1; break;
                case EDataType.Int:
                    RegisterInfo.Length = 2; break;
                case EDataType.Float:
                    RegisterInfo.Length = 2; break;
                case EDataType.Double:
                    RegisterInfo.Length = 4; break;
            }
            Address = Convert.ToInt32(pRegisterInfo.Address.ReplaceLetter());

        }
    }

    /// <summary>
    /// 寄存器类型
    /// </summary>
    public enum ERegisterType
    {
        B,
        M,
        W,
        D,
    }


    /// <summary>
    /// 寄存器数据类型
    /// </summary>
    public enum EDataType
    {
        Bit,
        Short,
        Int,
        Float,
        Double,
        String,
    }
}
