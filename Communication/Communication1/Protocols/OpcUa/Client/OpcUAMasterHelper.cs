using Communication.Core.Abstracts;
using Communication.Core.Definition;
using Communication.Tools.ValueDecoder;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Communication.Core.Definition.CommunicationParam;

namespace Communication.Protocols.OpcUa.Client
{
    /// <summary>
    /// OPC UA客户端辅助类
    /// </summary>
    public class OpcUAMasterHelper : DeviceCommunication
    {
        public OpcUAMasterHelper(IContainerProvider container) : base(container)
        {
        }

        #region 私有字段

        private bool mIsConnected;
        private Session mSession;
        private ApplicationConfiguration mAppConfig;
        private SemaphoreSlim mIOLock = new SemaphoreSlim(1, 1);

        private Dictionary<string, ICommunicationDataPoint> mNameIndex = new Dictionary<string, ICommunicationDataPoint>();

        /// <summary>
        /// 存储读取到的原始值
        /// </summary>
        private Dictionary<string, object> mValueCache = new Dictionary<string, object>();
        private Dictionary<string, DateTime> mTimestampCache = new Dictionary<string, DateTime>();

        /// <summary>
        /// OPC UA节点ID正则表达式
        /// 格式: ns=<namespace>;[s|i|g|b]=<identifier>
        /// </summary>
        private readonly Regex mNodeIdRegex = new Regex(
            @"^ns=(?<ns>\d+);(?<type>[sigb])=(?<id>.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        /// <summary>
        /// 服务器端点URL
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// 应用程序名称
        /// </summary>
        public string ApplicationName { get; set; } = "OpcUAClient";

        /// <summary>
        /// 是否使用安全连接
        /// </summary>
        public bool UseSecurity { get; set; } = false;

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 会话超时时间(毫秒)
        /// </summary>
        public uint SessionTimeout { get; set; } = 60000;

        #endregion

        #region 公共属性

        public override ICommunicationDataPoint this[string name]
        {
            get
            {
                if (mNameIndex.ContainsKey(name))
                {
                    return mNameIndex[name];
                }
                return null;
            }
        }

        public override bool IsConnected
        {
            get => mIsConnected;
            set => mIsConnected = value;
        }

        public override DeviceProtocolType CommunicationType => DeviceProtocolType.OPCUA;

        public override bool CanAutoRead => true;

        #endregion

        #region 连接与断开

        /// <summary>
        /// 连接到OPC UA服务器
        /// </summary>
        public override async Task Connect()
        {
            try
            {
                // 创建应用程序配置
                var appConfig = new ApplicationConfiguration
                {
                    ApplicationName = ApplicationName,
                    ApplicationUri = "urn:" + ApplicationName,
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier(),
                        AutoAcceptUntrustedCertificates = true,
                        AddAppCertToTrustedStore = true,
                        RejectSHA1SignedCertificates = false,
                        MinimumCertificateKeySize = 1024,
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = (int)SessionTimeout },
                    TraceConfiguration = new TraceConfiguration()
                };

                await appConfig.ValidateAsync(ApplicationType.Client);

                if (appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    appConfig.CertificateValidator.CertificateValidation += (s, e) =>
                    {
                        e.Accept = true;
                    };
                }

                mAppConfig = appConfig;

                // 创建会话
                var endpoint = new EndpointDescription(EndpointUrl);
                
                mSession = await Session.Create(
                    mAppConfig,
                    new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create()),
                    false,
                    ApplicationName,
                    (uint)SessionTimeout,
                    GetUserIdentity(),
                    null
                );

                mIsConnected = true;
            }
            catch (Exception ex)
            {
                mIsConnected = false;
                throw new Exception($"OPC UA连接失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public override void Disconnect()
        {
            try
            {
                mSession?.CloseAsync().Wait();
                mSession?.Dispose();
                mSession = null;
                mIsConnected = false;
            }
            catch (Exception ex)
            {
                throw new Exception($"OPC UA断开连接失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 数据点注册

        /// <summary>
        /// 添加数据点（通过Input字符串配置）
        /// </summary>
        /// <param name="point">数据点</param>
        public void AddDataPoint(ICommunicationDataPoint point)
        {
            var opcDataPoint = point as OpcUaDataPoint;
            if (opcDataPoint == null)
            {
                throw new Exception($"{point.Name}:{point.Input} 地址不是OpcUaDataPoint类型，无法进行转换。");
            }

            // 解析节点ID字符串
            var match = mNodeIdRegex.Match(opcDataPoint.Input);
            if (!match.Success)
            {
                throw new FormatException($"节点ID格式错误: {point.Name}:{point.Input}。应为 'ns=<namespace>;[s|i|g|b]=<identifier>' 格式。");
            }

            // 解析命名空间索引
            ushort namespaceIndex = Convert.ToUInt16(match.Groups["ns"].Value);
            
            // 解析标识符类型和值
            string identifierType = match.Groups["type"].Value;
            string identifier = match.Groups["id"].Value;

            // 填充DecodeData
            opcDataPoint.DecodeData.NamespaceIndex = namespaceIndex;
            opcDataPoint.DecodeData.IdentifierType = identifierType;
            opcDataPoint.DecodeData.Identifier = identifier;
            opcDataPoint.DecodeData.FullNodeId = opcDataPoint.Input;

            // 注册到索引
            if (!mNameIndex.ContainsKey(point.Name))
            {
                mNameIndex.Add(point.Name, point);
            }

            point.Panel = this;
        }

        /// <summary>
        /// 快速注册数据点（便捷方法）
        /// </summary>
        /// <param name="name">点位名称</param>
        /// <param name="nodeId">节点ID字符串，如 "ns=2;s=Device.Temperature"</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>数据点对象</returns>
        public OpcUaDataPoint RegisterPoint(string name, string nodeId, DataType dataType)
        {
            var dataPoint = new OpcUaDataPoint
            {
                Name = name,
                Input = nodeId,
                DataType = dataType,
                Panel = this
            };

            AddDataPoint(dataPoint);
            return dataPoint;
        }

        #endregion

        #region 读取操作

        /// <summary>
        /// 批量异步读取所有数据点
        /// </summary>
        public override async Task ReadAsync(CancellationToken ct = default)
        {
            if (!mIsConnected || mSession == null)
            {
                throw new InvalidOperationException("未连接到OPC UA服务器");
            }

            await mIOLock.WaitAsync(ct);
            try
            {
                var nodesToRead = new ReadValueIdCollection();
                var dataPoints = new List<OpcUaDataPoint>();

                foreach (var dp in mNameIndex.Values.Cast<OpcUaDataPoint>())
                {
                    // 从DecodeData构建NodeId
                    var nodeId = BuildNodeId(dp.DecodeData);
                    
                    nodesToRead.Add(new ReadValueId
                    {
                        NodeId = nodeId,
                        AttributeId = Attributes.Value
                    });
                    dataPoints.Add(dp);
                }

                var response = await mSession.ReadAsync(
                    null,
                    0,
                    TimestampsToReturn.Both,
                    nodesToRead,
                    ct
                );

                for (int i = 0; i < response.Results.Count; i++)
                {
                    if (StatusCode.IsGood(response.Results[i].StatusCode))
                    {
                        var dp = dataPoints[i];
                        mValueCache[dp.Name] = response.Results[i].Value;
                        mTimestampCache[dp.Name] = response.Results[i].SourceTimestamp;
                    }
                }
            }
            finally
            {
                mIOLock.Release();
            }
        }

        /// <summary>
        /// 异步读取单个数据点
        /// </summary>
        public override async Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default)
        {
            if (!mIsConnected || mSession == null)
            {
                throw new InvalidOperationException("未连接到OPC UA服务器");
            }

            await mIOLock.WaitAsync(ct);
            try
            {
                var opcDataPoint = dp as OpcUaDataPoint;
                if (opcDataPoint == null)
                {
                    throw new ArgumentException("无效的数据点类型");
                }

                // 从DecodeData构建NodeId
                var nodeId = BuildNodeId(opcDataPoint.DecodeData);

                var readValueId = new ReadValueId
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value
                };

                var response = await mSession.ReadAsync(
                    null,
                    0,
                    TimestampsToReturn.Both,
                    new ReadValueIdCollection { readValueId },
                    ct
                );

                if (StatusCode.IsGood(response.Results[0].StatusCode))
                {
                    mValueCache[opcDataPoint.Name] = response.Results[0].Value;
                    mTimestampCache[opcDataPoint.Name] = response.Results[0].SourceTimestamp;
                    return DecodeValue(opcDataPoint);
                }
                else
                {
                    throw new Exception($"读取失败: {response.Results[0].StatusCode}");
                }
            }
            finally
            {
                mIOLock.Release();
            }
        }

        #endregion

        #region 写入操作

        /// <summary>
        /// 批量异步写入数据
        /// </summary>
        public override async Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            if (!mIsConnected || mSession == null)
            {
                throw new InvalidOperationException("未连接到OPC UA服务器");
            }

            await mIOLock.WaitAsync();
            try
            {
                var nodesToWrite = new WriteValueCollection();

                foreach (var (dp, value) in pvs)
                {
                    var opcDataPoint = dp as OpcUaDataPoint;
                    if (opcDataPoint == null) continue;

                    // 从DecodeData构建NodeId
                    var nodeId = BuildNodeId(opcDataPoint.DecodeData);

                    var writeValue = new WriteValue
                    {
                        NodeId = nodeId,
                        AttributeId = Attributes.Value,
                        Value = new DataValue
                        {
                            Value = ConvertValue(value, opcDataPoint.DataType)
                        }
                    };
                    nodesToWrite.Add(writeValue);
                }

                var response = await mSession.WriteAsync(
                    null,
                    nodesToWrite,
                    CancellationToken.None
                );

                for (int i = 0; i < response.Results.Count; i++)
                {
                    if (!StatusCode.IsGood(response.Results[i]))
                    {
                        throw new Exception($"写入失败 [{i}]: {response.Results[i]}");
                    }
                }
            }
            finally
            {
                mIOLock.Release();
            }
        }

        /// <summary>
        /// 异步写入单个数据点
        /// </summary>
        public override async Task WriteAsync(ICommunicationDataPoint dp, object value)
        {
            await WriteAsync((dp, value));
        }

        /// <summary>
        /// 批量同步写入数据
        /// </summary>
        public override void Write(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            WriteAsync(pvs).Wait();
        }

        /// <summary>
        /// 同步写入单个数据点
        /// </summary>
        public override void Write(ICommunicationDataPoint dp, object value)
        {
            WriteAsync(dp, value).Wait();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从DecodeData构建NodeId对象
        /// </summary>
        private NodeId BuildNodeId(OpcUaDecodeData decodeData)
        {
            switch (decodeData.IdentifierType)
            {
                case "s": // 字符串标识符
                    return new NodeId(decodeData.Identifier, decodeData.NamespaceIndex);
                case "i": // 数字标识符
                    return new NodeId(Convert.ToUInt32(decodeData.Identifier), decodeData.NamespaceIndex);
                case "g": // GUID标识符
                    return new NodeId(Guid.Parse(decodeData.Identifier), decodeData.NamespaceIndex);
                case "b": // 字节数组标识符
                    return new NodeId(Convert.FromBase64String(decodeData.Identifier), decodeData.NamespaceIndex);
                default:
                    throw new Exception($"不支持的标识符类型: {decodeData.IdentifierType}");
            }
        }

        /// <summary>
        /// 解码数据点值
        /// </summary>
        public DeviceValue DecodeValue(OpcUaDataPoint dp)
        {
            if (!mValueCache.ContainsKey(dp.Name))
            {
                return new DeviceValue();
            }

            var rawValue = mValueCache[dp.Name];
            if (rawValue == null)
            {
                return new DeviceValue();
            }

            // 根据数据类型转换
            try
            {
                switch (dp.DataType)
                {
                    case DataType.BIT:
                        return Convert.ToBoolean(rawValue);
                    case DataType.BYTE:
                        return Convert.ToByte(rawValue);
                    case DataType.INT16:
                        return Convert.ToInt16(rawValue);
                    case DataType.UINT16:
                        return Convert.ToUInt16(rawValue);
                    case DataType.INT32:
                        return Convert.ToInt32(rawValue);
                    case DataType.UINT32:
                        return Convert.ToUInt32(rawValue);
                    case DataType.SINGLE:
                        return Convert.ToSingle(rawValue);
                    case DataType.DOUBLE:
                        return Convert.ToDouble(rawValue);
                    case DataType.UTF32:
                    case DataType.ASCII:
                        return rawValue.ToString() ?? string.Empty;
                    default:
                        return DeviceValue.FromObject(rawValue);
                }
            }
            catch
            {
                return DeviceValue.FromObject(rawValue);
            }
        }

        /// <summary>
        /// 转换写入值
        /// </summary>
        private object ConvertValue(object value, DataType dataType)
        {
            if (value == null) return null;

            try
            {
                switch (dataType)
                {
                    case DataType.BIT:
                        return Convert.ToBoolean(value);
                    case DataType.BYTE:
                        return Convert.ToByte(value);
                    case DataType.INT16:
                        return Convert.ToInt16(value);
                    case DataType.UINT16:
                        return Convert.ToUInt16(value);
                    case DataType.INT32:
                        return Convert.ToInt32(value);
                    case DataType.UINT32:
                        return Convert.ToUInt32(value);
                    case DataType.SINGLE:
                        return Convert.ToSingle(value);
                    case DataType.DOUBLE:
                        return Convert.ToDouble(value);
                    default:
                        return value;
                }
            }
            catch
            {
                return value;
            }
        }

        /// <summary>
        /// 获取用户身份
        /// </summary>
        private UserIdentity GetUserIdentity()
        {
            if (!string.IsNullOrEmpty(UserName))
            {
                return new UserIdentity(new UserNameIdentityToken
                {
                    UserName = UserName,
                    Password = System.Text.Encoding.UTF8.GetBytes(Password ?? "")
                });
            }
            return new UserIdentity(new AnonymousIdentityToken());
        }

        #endregion
    }
}
