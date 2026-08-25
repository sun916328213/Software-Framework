using Communication.Core.Abstracts;
using Communication.Core.Definition;
using System;
using static Communication.Core.Definition.CommunicationParam;

namespace Communication.Protocols.OpcUa.Client
{
    /// <summary>
    /// OPC UA数据点
    /// </summary>
    public class OpcUaDataPoint : ICommunicationDataPoint
    {
        ValueChangedDelegate valueChanged_Light;
        ValueChangedDelegate valueChanged_Heavy;

        DeviceCommunication mPanel;

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 输入的节点ID配置字符串
        /// 格式: "ns=2;s=Device.Temperature" 或 "ns=2;i=1234"
        /// </summary>
        public string Input { get; set; }

        public DataType DataType { get; set; }

        public int StrLength { get; set; }

        public ushort GetLength()
        {
            switch (DataType)
            {
                case DataType.BIT:
                case DataType.BYTE:
                case DataType.INT16:
                case DataType.UINT16:
                    return 1;
                case DataType.INT32:
                case DataType.UINT32:
                case DataType.SINGLE:
                    return 2;
                case DataType.DOUBLE:
                    return 4;
                case DataType.UTF32:
                    return (ushort)(StrLength * 2);
                case DataType.ASCII:
                    return (ushort)((StrLength + 2 - 1) / 2);
                default:
                    return 1;
            }
        }

        /// <summary>
        /// 解析后的节点ID数据
        /// </summary>
        public OpcUaDecodeData DecodeData { get; set; } = new OpcUaDecodeData();

        public int LastGeneration { get; set; }

        public DeviceCommunication Panel { set { mPanel = value; } }

        public void OnValueChanged(ValueChangedDelegate value, E_CallBackWeight weight = E_CallBackWeight.Light)
        {
            switch (weight)
            {
                case E_CallBackWeight.Light:
                    if (valueChanged_Light != null && valueChanged_Light.GetInvocationList().Contains(value))
                    {
                        return;
                    }
                    valueChanged_Light += value;
                    break;
                case E_CallBackWeight.Heavy:
                    if (valueChanged_Heavy != null && valueChanged_Heavy.GetInvocationList().Contains(value))
                    {
                        return;
                    }
                    valueChanged_Heavy += value;
                    break;
            }
        }

        public DeviceValue GetValue()
        {
            return ((OpcUAMasterHelper)mPanel).DecodeValue(this);
        }

        public void SetValue(object val)
        {
            ((OpcUAMasterHelper)mPanel).WriteAsync(this, val);
        }

        public ValueChangedDelegate GetValChangeDel_Light()
        {
            return valueChanged_Light;
        }

        public ValueChangedDelegate GetValChangeDel_Heavy()
        {
            return valueChanged_Heavy;
        }
    }

    /// <summary>
    /// OPC UA解析后的节点数据
    /// </summary>
    public class OpcUaDecodeData
    {
        /// <summary>
        /// 命名空间索引
        /// </summary>
        public ushort NamespaceIndex { get; set; }

        /// <summary>
        /// 标识符类型 (s=字符串, i=数字, g=GUID, b=字节数组)
        /// </summary>
        public string IdentifierType { get; set; }

        /// <summary>
        /// 标识符值
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// 完整的节点ID字符串
        /// </summary>
        public string FullNodeId { get; set; }
    }
}
