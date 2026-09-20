using Communication;
using Communication.Communication.Socket;
using Communication.Communication.Socket.HPSocket;
using Communication.Core.Definition;
using Communication.Management;
using Communication.Protocols.Mitsubishi;
using Communication.Protocols.Modbus;
using Communication.Protocols.Modbus.TCP;
using Communication.Protocols.OpcUa.Client;
using Communication.Protocols.OpcUa.Server;
using Extension.Core.Conversion;
using Prism.Ioc;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using static Communication.Core.Definition.CommunicationParam;

namespace CommunicationDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        IContainerProvider ContainerProvider { get; set; }
        public MainWindow(IContainerProvider container)
        {
            InitializeComponent();
            ContainerProvider = container;

            CommPortManager commPortManager = container.Resolve<CommPortManager>();
            commPortManager.AddPort("1", new HPSocketClientHelper("127.0.0.1", 5511));
        }

        #region HPSocket Client

        SocketBase HPSocketClientHelper = new HPSocketClientHelper();

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            HPSocketClientHelper.SocketParam.RemoteIP = "127.0.0.1";
            HPSocketClientHelper.SocketParam.RemotePort = 502;
            HPSocketClientHelper.OnDataReceived += (s, e1) =>
            {
                var data = e1.ToArray().ToASCII(EEndianType.Little_Endian).ToString();
                // 使用Dispatcher.Invoke在UI线程上更新控件
                this.Dispatcher.Invoke(() =>
                {
                    tbk_ReceivedData.Text = data;
                });
            };
            HPSocketClientHelper.Connect();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            string buffer = "12345678";

            HPSocketClientHelper.WriteOnlyAsync(buffer.ToBytes(Encoding.UTF8));
        }

        private async void Button4_Click(object sender, RoutedEventArgs e)
        {
            string buffer = "12345678";

            var d1 = await HPSocketClientHelper.WriteRequestAsync(buffer.ToBytes(Encoding.UTF8), 10000);
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            HPSocketClientHelper.Disconnect();
        }

        #endregion

        #region Mitsubishi
        MC3EHelper mitsubis3E;

        McDataPoint mp1 = new McDataPoint()
        {
            Name = "测试D100",
            Input = "D100",
            DataType = DataType.INT16
        };
        McDataPoint mp2 = new McDataPoint()
        {
            Name = "测试M100",
            Input = "M100",
            DataType = DataType.BIT
        };

        McDataPoint mp3 = new McDataPoint()
        {
            Name = "报警",
            Input = "D201",
            DataType = DataType.INT16
        };
        private void ButtonMitsubishi1_Click(object sender, RoutedEventArgs e)
        {
            mitsubis3E = ContainerProvider.Resolve<MC3EHelper>("test_MC3E");

            mitsubis3E.Config("1");
            mitsubis3E.ConfigValueArray(E_McDeviceCode.M, 1500);
            mitsubis3E.ConfigValueArray(E_McDeviceCode.D, 1500);

            mitsubis3E.AddDataPoint(mp1);
            mitsubis3E.AddDataPoint(mp2);
            mitsubis3E.AddDataPoint(mp3);
            mitsubis3E.Connect();


        }


        private void ButtonMitsubishi2_Click(object sender, RoutedEventArgs e)
        {
            mitsubis3E.Write(mp1, 23);
            mitsubis3E.Write(mp2, true);
        }

        private async void ButtonMitsubishi3_Click(object sender, RoutedEventArgs e)
        {
            var d1 = await mitsubis3E.ReadAsync(mp1);
            tb_Value.Text = d1.ToString();

            //var d2 = await mitsubis3E.ReadAsync(mp2);
            //tb_Value.Text = d2.ToString();
        }

        private void ButtonMitsubishi4_Click(object sender, RoutedEventArgs e)
        {
            mitsubis3E.Disconnect();
        }

        private void ButtonMitsubishi5_Click(object sender, RoutedEventArgs e)
        {
            mitsubis3E.SortOrderReadList();
            mitsubis3E.StartAutoRead();
        }

        private async void ButtonMitsubishi6_Click(object sender, RoutedEventArgs e)
        {
            await mitsubis3E.StopAutoRead();
        }

        private async void ButtonMitsubishi7_Click(object sender, RoutedEventArgs e)
        {
            await mitsubis3E.WriteAsync((mp1, 100), (mp2, 1));
        }

        #endregion

        #region ModbusTCP

        ModbusTCP_MasterHelper modbusTCP_MasterHelper;

        // 定义数据点
        ModbusDataPoint modbusPoint1 = new ModbusDataPoint()
        {
            Name = "保持寄存器1",
            Input = "40001",
            DataType = DataType.INT16
        };

        ModbusDataPoint modbusPoint2 = new ModbusDataPoint()
        {
            Name = "保持寄存器2",
            Input = "40002",
            DataType = DataType.UINT16
        };

        ModbusDataPoint modbusPoint3 = new ModbusDataPoint()
        {
            Name = "线圈1",
            Input = "00001",
            DataType = DataType.BIT
        };

        ModbusDataPoint modbusPoint4 = new ModbusDataPoint()
        {
            Name = "温度值",
            Input = "40010",
            DataType = DataType.SINGLE  // 浮点数占2个寄存器
        };

        private void ButtonModbusTCP1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建ModbusTCP客户端
                modbusTCP_MasterHelper = new ModbusTCP_MasterHelper(ContainerProvider)
                {
                    Name = "ModbusTCP_Demo",
                    ID = "Modbus001",
                    IPAddress = "127.0.0.1",
                    Port = 502,
                    SlaveID = 1
                };

                // 设置数组大小
                modbusTCP_MasterHelper.SetNum(
                    coilNum: 200,              // 线圈数量
                    discreteInputsNum: 200,    // 离散输入数量
                    holdingRegistersNum: 500,  // 保持寄存器数量
                    inputRegistersNum: 200     // 输入寄存器数量
                );

                // 添加数据点
                modbusTCP_MasterHelper.AddDataPoint(modbusPoint1);
                modbusTCP_MasterHelper.AddDataPoint(modbusPoint2);
                modbusTCP_MasterHelper.AddDataPoint(modbusPoint3);
                modbusTCP_MasterHelper.AddDataPoint(modbusPoint4);

                // 连接
                modbusTCP_MasterHelper.Connect();

                MessageBox.Show("ModbusTCP连接成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonModbusTCP2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (modbusTCP_MasterHelper == null)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 排序读取列表
                modbusTCP_MasterHelper.SortOrderReadList();

                // 批量读取所有数据点
                await modbusTCP_MasterHelper.ReadAsync();

                // 显示值
                var value1 = modbusPoint1.GetValue();
                var value2 = modbusPoint2.GetValue();
                var value3 = modbusPoint3.GetValue();
                var value4 = modbusPoint4.GetValue();

                tb_ModbusTCPValue.Text = $"40001={value1}, 40002={value2}, 00001={value3}, 40010={value4}";

                MessageBox.Show("批量读取成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonModbusTCP3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (modbusTCP_MasterHelper == null)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 根据输入的寄存器地址创建临时数据点
                string registerAddress = tb_ModbusTCPRegister.Text;
                var tempPoint = new ModbusDataPoint
                {
                    Name = "TempPoint",
                    Input = registerAddress,
                    DataType = DataType.INT16
                };
                modbusTCP_MasterHelper.AddDataPoint(tempPoint);

                // 批量读取
                modbusTCP_MasterHelper.SortOrderReadList();
                await modbusTCP_MasterHelper.ReadAsync();

                // 显示值
                tb_ModbusTCPValue.Text = tempPoint.GetValue().ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonModbusTCP4_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (modbusTCP_MasterHelper == null)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(tb_ModbusTCPWriteValue.Text))
                {
                    MessageBox.Show("请输入写入值！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 根据输入的寄存器地址创建临时数据点
                string registerAddress = tb_ModbusTCPRegister.Text;
                var tempPoint = new ModbusDataPoint
                {
                    Name = "TempWritePoint",
                    Input = registerAddress,
                    DataType = DataType.INT16
                };
                modbusTCP_MasterHelper.AddDataPoint(tempPoint);

                // 单点写入
                short writeValue = short.Parse(tb_ModbusTCPWriteValue.Text);
                modbusTCP_MasterHelper.Write(tempPoint, writeValue);

                MessageBox.Show("写入成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonModbusTCP5_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (modbusTCP_MasterHelper != null)
                {
                    modbusTCP_MasterHelper.Disconnect();
                    MessageBox.Show("已断开连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"断开失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonModbusTCP6_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (modbusTCP_MasterHelper == null)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(tb_ModbusTCPWriteValue.Text))
                {
                    MessageBox.Show("请输入写入值！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                short writeValue = short.Parse(tb_ModbusTCPWriteValue.Text);

                // 多点批量写入
                modbusTCP_MasterHelper.Write(
                    (modbusPoint1, writeValue),
                    (modbusPoint2, (ushort)200),
                    (modbusPoint3, true)
                );

                MessageBox.Show("多点写入成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonModbusTCP7_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (modbusTCP_MasterHelper == null)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 排序读取列表
                modbusTCP_MasterHelper.SortOrderReadList();

                // 启动自动循环读取
                modbusTCP_MasterHelper.StartAutoRead();

                MessageBox.Show("循环读取已启动！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonModbusTCP8_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (modbusTCP_MasterHelper != null)
                {
                    await modbusTCP_MasterHelper.StopAutoRead();
                    MessageBox.Show("循环读取已停止！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region MC
        MCHelper mCHelper;
        private void ButtonMC1_Click(object sender, RoutedEventArgs e)
        {
            mCHelper = ContainerProvider.Resolve<MCHelper>("test_MC");
            mCHelper.Config(1, null);
            mCHelper.ConfigValueArray(E_McDeviceCode.M, 1500);
            mCHelper.ConfigValueArray(E_McDeviceCode.D, 1500);

            mCHelper.AddDataPoint(mp1);
            mCHelper.AddDataPoint(mp2);
            mCHelper.AddDataPoint(mp3);
            mCHelper.Connect();

            mp1.OnValueChanged(x =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tb_MCValue.Text = x.ToString();
                });
            });

            mp2.OnValueChanged(x =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tb_MCValue.Text = x.ToString();
                });
            });

            mp3.OnValueChanged(x =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tb_MCValue.Text = x.ToString();
                });
            });
        }

        private void ButtonMC2_Click(object sender, RoutedEventArgs e)
        {
            mCHelper.Write(mp1, 11);
        }

        private async void ButtonMC3_Click(object sender, RoutedEventArgs e)
        {
            var d1 = await mCHelper.ReadAsync(mp1);


            var d2 = await mCHelper.ReadAsync(mp2);

        }

        private void ButtonMC4_Click(object sender, RoutedEventArgs e)
        {
            mCHelper.Disconnect();
        }

        private void ButtonMC5_Click(object sender, RoutedEventArgs e)
        {
            mCHelper.SortOrderReadList();
            mCHelper.StartAutoRead();
        }


        private async void ButtonMC6_Click(object sender, RoutedEventArgs e)
        {
            await mCHelper.StopAutoRead();
        }

        private async void ButtonMC7_Click(object sender, RoutedEventArgs e)
        {
            await mCHelper.WriteAsync((mp1, 100), (mp2, 1));
        }


        #endregion

        #region OPC UA Client

        OpcUAMasterHelper opcUaClient;
        OpcUaDataPoint opcUaPoint1;
        OpcUaDataPoint opcUaPoint2;

        private async void ButtonOpcUaClient1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建OPC UA客户端
                opcUaClient = new OpcUAMasterHelper(ContainerProvider)
                {
                    Name = "OPC_UA_Client",
                    ID = "OPC001",
                    EndpointUrl = "opc.tcp://localhost:4840",
                    ApplicationName = "DemoClient",
                    UseSecurity = false
                };

                // 注册数据点（MC风格配置）
                opcUaPoint1 = opcUaClient.RegisterPoint(
                    name: "Temperature",
                    nodeId: "ns=2;s=Device.Temperature",
                    dataType: DataType.SINGLE
                );

                opcUaPoint2 = opcUaClient.RegisterPoint(
                    name: "Status",
                    nodeId: "ns=2;s=Device.Status",
                    dataType: DataType.INT32
                );

                // 连接服务器
                await opcUaClient.Connect();

                MessageBox.Show("OPC UA客户端连接成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonOpcUaClient2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (opcUaClient == null || !opcUaClient.IsConnected)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 批量读取所有数据点
                await opcUaClient.ReadAsync();

                // 显示第一个点的值
                var value = opcUaPoint1.GetValue();
                tb_OpcUaClientValue.Text = value.ToString();

                MessageBox.Show("批量读取成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonOpcUaClient3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (opcUaClient == null || !opcUaClient.IsConnected)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 使用输入的节点ID创建临时数据点
                var tempPoint = new OpcUaDataPoint
                {
                    Name = "TempPoint",
                    Input = tb_OpcUaClientNodeId.Text,
                    DataType = DataType.SINGLE
                };
                opcUaClient.AddDataPoint(tempPoint);

                // 单点读取
                var value = await opcUaClient.ReadAsync(tempPoint);
                tb_OpcUaClientValue.Text = value.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonOpcUaClient4_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (opcUaClient == null || !opcUaClient.IsConnected)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(tb_OpcUaClientWriteValue.Text))
                {
                    MessageBox.Show("请输入写入值！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 使用输入的节点ID创建临时数据点
                var tempPoint = new OpcUaDataPoint
                {
                    Name = "TempWritePoint",
                    Input = tb_OpcUaClientNodeId.Text,
                    DataType = DataType.SINGLE
                };
                opcUaClient.AddDataPoint(tempPoint);

                // 单点写入
                float writeValue = float.Parse(tb_OpcUaClientWriteValue.Text);
                await opcUaClient.WriteAsync(tempPoint, writeValue);

                MessageBox.Show("写入成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonOpcUaClient5_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (opcUaClient != null)
                {
                    opcUaClient.Disconnect();
                    MessageBox.Show("已断开连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"断开失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonOpcUaClient6_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (opcUaClient == null || !opcUaClient.IsConnected)
                {
                    MessageBox.Show("请先连接！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(tb_OpcUaClientWriteValue.Text))
                {
                    MessageBox.Show("请输入写入值！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                float writeValue = float.Parse(tb_OpcUaClientWriteValue.Text);

                // 多点批量写入
                await opcUaClient.WriteAsync(
                    (opcUaPoint1, writeValue),
                    (opcUaPoint2, 1)
                );

                MessageBox.Show("多点写入成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region OPC UA Server

        OpcUAServerHelper opcUaServer;

        private async void ButtonOpcUaServer1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                opcUaServer = new OpcUAServerHelper
                {
                    ServerName = "DemoOpcUaServer",
                    EndpointUrl = "opc.tcp://localhost:4840"
                };

                await opcUaServer.StartAsync();

                tb_ServerStatus.Text = "运行中";
                tb_ServerStatus.Foreground = new SolidColorBrush(Colors.Green);
                tb_ServerEndpoint.Text = opcUaServer.EndpointUrl;

                MessageBox.Show("OPC UA服务器启动成功！\n端点地址: " + opcUaServer.EndpointUrl, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonOpcUaServer2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (opcUaServer == null || !opcUaServer.IsRunning)
                {
                    MessageBox.Show("请先启动服务器！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(tb_ServerNodeName.Text))
                {
                    MessageBox.Show("请输入节点名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string nodeName = tb_ServerNodeName.Text;
                float value = float.Parse(tb_ServerNodeValue.Text);

                opcUaServer.AddVariableNode(nodeName, value);

                MessageBox.Show($"节点 {nodeName} 添加成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonOpcUaServer3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (opcUaServer == null || !opcUaServer.IsRunning)
                {
                    MessageBox.Show("请先启动服务器！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(tb_ServerNodeName.Text))
                {
                    MessageBox.Show("请输入节点名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string nodeName = tb_ServerNodeName.Text;
                float value = float.Parse(tb_ServerNodeValue.Text);

                opcUaServer.UpdateNodeValue(nodeName, value);

                MessageBox.Show($"节点 {nodeName} 值更新为 {value}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonOpcUaServer4_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (opcUaServer != null)
                {
                    opcUaServer.Stop();
                    tb_ServerStatus.Text = "未启动";
                    tb_ServerStatus.Foreground = new SolidColorBrush(Colors.Red);

                    MessageBox.Show("服务器已停止！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
