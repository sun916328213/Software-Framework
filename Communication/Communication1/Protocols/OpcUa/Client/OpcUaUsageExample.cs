using Communication.Core.Definition;
using Communication.Protocols.OpcUa;
using Prism.Ioc;
using System;
using System.Threading.Tasks;
using static Communication.Core.Definition.CommunicationParam;

namespace Communication.Protocols.OpcUa.Client.Examples
{
    /// <summary>
    /// OPC UA使用示例
    /// </summary>
    public class OpcUaUsageExample
    {
        /// <summary>
        /// 基本使用示例
        /// </summary>
        public static async Task BasicUsageExample(IContainerProvider container)
        {
            // 1. 创建OPC UA客户端实例
            var opcUaClient = new OpcUAMasterHelper(container)
            {
                Name = "OPCUA_Server_1",
                ID = "OPC001",
                EndpointUrl = "opc.tcp://localhost:4840",  // OPC UA服务器地址
                ApplicationName = "MyClient",
                UseSecurity = false,  // 不使用安全连接
                SessionTimeout = 60000  // 会话超时时间(毫秒)
            };

            // 如果需要用户名密码认证
            // opcUaClient.UserName = "admin";
            // opcUaClient.Password = "password";

            // 2. 方式一：使用RegisterPoint快速注册（推荐）
            var temperaturePoint = opcUaClient.RegisterPoint(
                name: "Temperature",
                nodeId: "ns=2;s=Device.Temperature",  // OPC UA节点ID
                dataType: DataType.SINGLE
            );

            var pressurePoint = opcUaClient.RegisterPoint(
                name: "Pressure",
                nodeId: "ns=2;s=Device.Pressure",
                dataType: DataType.SINGLE
            );

            // 2. 方式二：使用AddDataPoint添加（与MC协议一致的格式）
            var statusPoint = new OpcUaDataPoint
            {
                Name = "MachineStatus",
                Input = "ns=2;s=Device.Status",  // 通过Input配置节点ID
                DataType = DataType.INT32
            };
            opcUaClient.AddDataPoint(statusPoint);

            var alarmPoint = new OpcUaDataPoint
            {
                Name = "AlarmFlag",
                Input = "ns=2;s=Device.Alarm",
                DataType = DataType.BIT
            };
            opcUaClient.AddDataPoint(alarmPoint);

            // 3. 连接到OPC UA服务器
            await opcUaClient.Connect();
            Console.WriteLine($"连接状态: {opcUaClient.IsConnected}");

            // 4. 批量读取所有数据点
            await opcUaClient.ReadAsync();

            // 5. 获取单个数据点的值
            var tempValue = temperaturePoint.GetValue();
            Console.WriteLine($"温度: {tempValue}");

            var pressureValue = pressurePoint.GetValue();
            Console.WriteLine($"压力: {pressureValue}");

            // 6. 通过索引器获取并读取单个数据点
            var point = opcUaClient["MachineStatus"];
            var statusValue = await opcUaClient.ReadAsync(point);
            Console.WriteLine($"机器状态: {statusValue}");

            // 7. 写入单个数据点
            opcUaClient.Write(alarmPoint, true);  // 同步写入
            await opcUaClient.WriteAsync(alarmPoint, false);  // 异步写入

            // 8. 批量写入数据点
            opcUaClient.Write(
                (temperaturePoint, 25.5f),
                (pressurePoint, 101.3f)
            );

            await opcUaClient.WriteAsync(
                (temperaturePoint, 26.0f),
                (pressurePoint, 101.5f)
            );

            // 9. 断开连接
            opcUaClient.Disconnect();
        }

        /// <summary>
        /// 支持的节点ID格式示例
        /// </summary>
        public static void NodeIdFormatExamples()
        {
            // OPC UA支持多种节点ID格式:

            // 1. 字符串标识符 (最常用)
            string nodeId1 = "ns=2;s=MyVariable";          // 命名空间索引=2, 字符串标识符
            string nodeId2 = "ns=2;s=Device.Temperature";  // 支持层级路径

            // 2. 数字标识符
            string nodeId3 = "ns=2;i=1234";                // 命名空间索引=2, 数字标识符=1234

            // 3. GUID标识符
            string nodeId4 = "ns=2;g=550e8400-e29b-41d4-a716-446655440000";

            // 4. 字节数组标识符
            string nodeId5 = "ns=2;b=YWJjZGVm";             // Base64编码

            // 常见OPC UA服务器的节点ID示例:
            // - Kepware: "ns=2;s=Channel1.Device1.Tag1"
            // - Siemens: "ns=3;s=\"DB1\".Temperature"
            // - Beckhoff: "ns=4;s=MAIN.nVar"
        }

        /// <summary>
        /// 支持的数据类型示例
        /// </summary>
        public static void DataTypeExamples()
        {
            // OPC UA支持以下数据类型:
            // - DataType.BIT      : 布尔值 (true/false)
            // - DataType.BYTE     : 字节 (0-255)
            // - DataType.INT16    : 16位有符号整数
            // - DataType.UINT16   : 16位无符号整数
            // - DataType.INT32    : 32位有符号整数
            // - DataType.UINT32   : 32位无符号整数
            // - DataType.SINGLE   : 32位浮点数
            // - DataType.DOUBLE   : 64位浮点数
            // - DataType.UTF32    : UTF32编码字符串
            // - DataType.ASCII    : ASCII编码字符串
        }

        /// <summary>
        /// 带错误处理的完整示例
        /// </summary>
        public static async Task CompleteExampleWithErrorHandling(IContainerProvider container)
        {
            OpcUAMasterHelper opcUaClient = null;

            try
            {
                // 创建客户端
                opcUaClient = new OpcUAMasterHelper(container)
                {
                    Name = "Production_Server",
                    ID = "OPC_PROD_001",
                    EndpointUrl = "opc.tcp://192.168.1.100:4840",
                    UseSecurity = false
                };

                // 注册数据点（MC风格）
                opcUaClient.RegisterPoint("Speed", "ns=2;s=Motor.Speed", DataType.SINGLE);
                opcUaClient.RegisterPoint("Torque", "ns=2;s=Motor.Torque", DataType.SINGLE);

                // 连接
                await opcUaClient.Connect();

                // 读取数据
                await opcUaClient.ReadAsync();

                var speedValue = opcUaClient["Speed"].GetValue();
                if (speedValue.TryGet<float>(out float speed))
                {
                    Console.WriteLine($"电机转速: {speed} RPM");
                }

                // 写入数据
                opcUaClient.Write(opcUaClient["Speed"], 1500.0f);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OPC UA通讯错误: {ex.Message}");
            }
            finally
            {
                // 确保断开连接
                opcUaClient?.Disconnect();
            }
        }
    }
}
