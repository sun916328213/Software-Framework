# Device.Drivers

该项目只包含新架构的厂商 SDK 适配器，不引用旧 `Device` 项目。

整体实现沿用原 `Device` 的直接组织思路：每类设备只提供一个统一 Factory，
具体品牌由该 Factory 集中创建，不为每个品牌再建立一套工厂或额外项目。
Core 与 Drivers 是当前已有结构，后续新增设备类型时不再继续拆分项目。

## 平台要求

`Device.Drivers` 是 `AnyCPU` 类库，不替最终应用决定进程架构。最终应用可以根据实际使用的
设备驱动选择 `AnyCPU` 或 `x64`；在 64 位操作系统上，未启用 32 位优先的 AnyCPU 应用通常
会以 64 位进程运行。

相机、采集卡、打印软件和光源控制器仍需要安装或随应用部署各厂商要求的运行库。
如果所选厂商 SDK 或 Interop 只提供 64 位版本，最终应用必须以 64 位进程运行；AnyCPU
只表示 Device 类库不固定架构，并不能把 64 位原生 SDK 转换为 32 位组件。
Factory 只在 `Create` 时构造适配器，不会提前加载全部厂商 SDK；未使用某个品牌时，
不会主动连接、枚举或初始化该品牌的硬件。

当前已经独立实现海康、Basler、大华和 I-Tek GigE 相机驱动：

| 配置值 | 品牌 | 接口 |
| --- | --- | --- |
| `camera.hik.gige` | Hikvision | GigE |
| `camera.basler.gige` | Basler | GigE |
| `camera.dahua.gige` | Dahua | GigE |
| `camera.itek.gige` | I-Tek | GigE |

```csharp
var factory = new CameraFactory();
var choices = factory.AvailableDrivers;
var selectedDriver = choices[0]; // 实际界面中由用户从 AvailableDrivers 选择

var camera = factory.Create(new CameraOptions
{
    Name = "Camera01",
    DriverId = selectedDriver.Id,
    IpAddress = "192.168.1.10",
});
```

配置界面应直接绑定 `factory.AvailableDrivers`。所有品牌统一由一个
`CameraFactory` 创建；海康、大华等类型只作为工厂内部适配器存在。相机驱动标识只在
`CameraFactory` 的内部注册表中声明一次，新增品牌不需要再修改 Core 中的集中常量类。

品牌选择控件显示 `DeviceDriverDescriptor.DisplayName`，保存时写入
`DeviceDriverDescriptor.Id`。配置文件只保存稳定字符串，例如：

```json
{
  "Name": "Camera01",
  "DriverId": "camera.dahua.gige",
  "IpAddress": "192.168.1.10",
  "ExposureMilliseconds": 20,
  "Gain": 0,
  "TimeoutMilliseconds": 1000
}
```

因此配置相机时仍然可以选择品牌，但领域模型和配置文件不需要品牌枚举。

### 面阵、线扫与触发配置

相机配置通过 `CameraScanType` 枚举区分面阵和线扫。品牌是开放集合，继续使用驱动标识；
扫描类型是稳定的封闭集合，使用枚举可以在编译期避免无效值。线扫相机可以提供厂商参数文件，
驱动在打开设备后先加载文件，再应用 `CameraOptions` 中的曝光、增益和触发配置。
帧触发与行触发分开保存，因此可以表达“外部信号开始一帧、编码器触发每一行”的常见线扫流程：

```csharp
var lineScanOptions = new CameraOptions
{
    Name = "LineCamera01",
    DriverId = selectedDriver.Id,
    IpAddress = "192.168.1.20",
    ScanType = CameraScanType.LineScan,
    ParameterFilePath = @"CameraConfig\LineScan.cfg",
    ExposureMilliseconds = 1,
    Gain = 0,
    FrameTrigger = new CameraTriggerSettings
    {
        Selector = CameraTriggerSelectors.FrameStart,
        Mode = CameraTriggerMode.On,
        Source = CameraTriggerSources.Line1,
    },
    LineTrigger = new CameraTriggerSettings
    {
        Selector = CameraTriggerSelectors.LineStart,
        Mode = CameraTriggerMode.On,
        Source = CameraTriggerSources.RotaryEncoder1,
    },
};
```

面阵相机默认使用帧开始软件触发。设置为外部触发或自由运行后，`CaptureAsync` 不再发送
软件触发命令，而是等待相机回调的下一帧。各型号支持的触发源不同；常用值由
`CameraTriggerSources` 提供，特殊型号也可以保存其 SDK 支持的 GenICam 触发源符号。
如果线扫型号使用 `FrameBurstStart` 而不是 `FrameStart`，可通过
`CameraTriggerSettings.Selector` 显式指定，不需要修改驱动代码。

## 光源控制器

光源模块采用相同方式，由一个 `LightControllerFactory` 提供全部品牌：

| 配置值 | 品牌 | 连接方式 |
| --- | --- | --- |
| `light.cst.serial` | CST | Serial |
| `light.opt.serial` | OPT | Serial |

```csharp
var factory = new LightControllerFactory();
var choices = factory.AvailableDrivers;
var cstDriver = choices.Single(x => x.Brand == "CST");

var light = factory.Create(new LightControllerOptions
{
    Name = "Light01",
    DriverId = cstDriver.Id,
    PortName = "COM3",
    ChannelCount = 4,
    MaximumIntensity = 255,
});

await light.ConnectAsync();
await light.SetIntensityAsync(channel: 1, intensity: 120);
await light.SetAllIntensityAsync(intensity: 0);
await light.DisconnectAsync();
```

配置界面显示 `AvailableDrivers` 中的品牌信息，配置文件只保存稳定的驱动标识，
因此新增光源品牌同样不需要修改枚举。

## 扫码器

当前扫码器工厂提供新大陆 NLS-NVH220 串口驱动：

| 配置值 | 品牌 | 型号 |
| --- | --- | --- |
| `scanner.newland.nvh220.serial` | Newland | NLS-NVH220 |

```csharp
var factory = new ScannerFactory();
var newlandDriver = factory.AvailableDrivers.Single(x => x.Brand == "Newland");
var scanner = factory.Create(new ScannerOptions
{
    Name = "Scanner01",
    DriverId = newlandDriver.Id,
    PortName = "COM3",
    BaudRate = 9600,
    Delimiter = "\r\n",
});

scanner.ScanReceived += (_, data) => Console.WriteLine(data.Value);
await scanner.ConnectAsync();
```

NVH220 为被动上报型设备，旧实现中的 `Trigger` 只会抛出“不支持”异常，
因此新接口不暴露无效的软件触发成员。

## RFID

RFID 设备统一由 `RfidDeviceFactory` 创建：

| 配置值 | 品牌 | 型号与协议 |
| --- | --- | --- |
| `rfid.ck.s650.modbus-rtu` | CK | S650 / Modbus RTU |
| `rfid.xinuo.xk15a-r01.serial` | Xinuo | XK15A-R01 / Serial |

```csharp
var factory = new RfidDeviceFactory();
var ckDriver = factory.AvailableDrivers.Single(x => x.Brand == "CK");
var rfid = factory.Create(new RfidOptions
{
    Name = "Rfid01",
    DriverId = ckDriver.Id,
    PortName = "COM3",
    SlaveId = 1,
});

await rfid.ConnectAsync();
var read = await rfid.ReadPagesAsync(startPage: 1, pageCount: 2);
var write = await rfid.WritePagesAsync(startPage: 1, ["PAGE-001", "PAGE-002"]);
await rfid.DisconnectAsync();
```

两种协议都对外提供同一组按页读写操作。每页固定为 8 字节，短页自动补零，
超过限制的数据在进入厂商协议前直接返回配置明确的失败结果。

## 打印机

打印机统一由 `PrinterFactory` 创建。目前注册 SATO SBPL 和 Zebra ZPL 两种网络驱动：

| 配置值 | 品牌 | 打印语言与连接方式 |
| --- | --- | --- |
| `printer.sato.sbpl.tcp` | SATO | SBPL / TCP |
| `printer.zebra.zpl.tcp` | Zebra | ZPL / TCP |

```csharp
var factory = new PrinterFactory();
var zebraDriver = factory.AvailableDrivers.Single(x => x.Brand == "Zebra");
var printer = factory.Create(new PrinterOptions
{
    Name = "Printer01",
    DriverId = zebraDriver.Id,
    IpAddress = "192.168.1.100",
    Port = 9100,
});

await printer.ConnectAsync();
var status = await printer.GetStatusAsync();
var print = await printer.PrintRawAsync("^XA^FO50,50^FDHello^FS^XZ"u8.ToArray());
await printer.DisconnectAsync();
```

状态查询只向真实打印机发送一次请求，再把厂商状态快照映射为公共 `PrinterStatus`。
SATO 驱动直接发送 SBPL 数据，并使用官方 Status4 `ENQ (0x05)` 状态查询协议；打印机对应网络端口
需要启用 `Status4 ENQ`。驱动能够跳过端口 9100 标准模式或旧版兼容模式附加的长度前缀。
旧项目代码保持不变。

```csharp
var statusResult = await printer.GetStatusAsync();
if (!statusResult.Success)
    throw new InvalidOperationException(statusResult.Message);

var status = statusResult.Value!;
if (status.PaperOut)
    Console.WriteLine("标签纸已用完");
if (status.RibbonOut)
    Console.WriteLine("色带已用完");
if (status.MediaNearEnd)
    Console.WriteLine("标签或色带即将用完，请提前准备耗材");
if (status.AdditionalFaultCode is not null)
    Console.WriteLine($"打印机故障：{status.AdditionalFaultCode}");
Console.WriteLine($"当前批次剩余标签：{status.LabelsRemainingInBatch}");
```

状态查询或打印传输发生 I/O、SDK 或协议错误后，设备状态会切换为 `Faulted`，调用方应先
断开或重新连接，避免继续使用可能包含残缺打印数据或迟到状态响应的连接。

## 相机采集卡

采集卡统一由 `FrameGrabberFactory` 创建：

| 配置值 | 品牌与驱动 |
| --- | --- |
| `frame-grabber.euresys.multicam` | Euresys MultiCam |
| `frame-grabber.matrox.radient-evcl` | Matrox Radient eV-CL |

```csharp
var factory = new FrameGrabberFactory();
var euresysDriver = factory.AvailableDrivers.Single(x => x.Brand == "Euresys");
var grabber = factory.Create(new FrameGrabberOptions
{
    Name = "Grabber01",
    DriverId = euresysDriver.Id,
    ConfigurationFilePath = "Camera.cam",
    BoardIndex = 0,
    Connector = "A",
    PixelFormat = "Mono16",
});

grabber.FrameReceived += (_, args) =>
{
    var frame = args.Frame;
    Console.WriteLine($"{frame.Sequence}: {frame.Width}x{frame.Height}, stride={frame.Stride}");
};

await grabber.ConnectAsync();
```

厂商回调中的原生缓冲区会先复制到托管内存，再发布 `FrameReceived`。旧实现中被注释掉的
图像事件已经恢复，同时移除了 Matrox 回调内固定的 500 毫秒休眠。Matrox 当前明确支持
`Mono8`；其他像素格式在创建阶段直接拒绝，避免按错误字节宽度复制导致图像损坏。

## 标签打印软件

标签打印软件与物理打印机分开建模。`PrinterFactory` 管理 Zebra 等物理设备连接，
`LabelPrintEngineFactory` 管理 BTW 模板、字段、预览和打印流程：

| 配置值 | 软件 |
| --- | --- |
| `label-print.bartender.sdk` | Seagull Scientific BarTender |

```csharp
var factory = new LabelPrintEngineFactory();
var barTenderDriver = factory.AvailableDrivers.Single();
await using var engine = factory.Create(new LabelPrintEngineOptions
{
    Name = "LabelEngine01",
    DriverId = barTenderDriver.Id,
    DefaultPrinterName = "Zebra ZT411",
});

await engine.OpenTemplateAsync("Labels/Product.btw");
await engine.SetFieldsAsync(new Dictionary<string, string>
{
    ["ProductName"] = "Example",
    ["SerialNumber"] = "SN-0001",
});
var preview = await engine.RenderPreviewAsync();
var print = await engine.PrintAsync(copies: 1);
```

适配器不会在构造时启动 BarTender，也不会强制结束用户正在运行的 BarTender 进程。
预览使用唯一临时 PNG 文件，读取到托管内存后立即清理。旧 `SpreadHelper` 全部为注释代码，
没有可迁移的运行实现，因此没有注册为可用驱动。

## 设备生命周期边界

Factory 只负责验证配置并创建一个尚未连接的设备实例，不在构造过程中访问真实硬件。
Controls 或具体应用负责决定创建时机，并显式调用 `ConnectAsync`、`DisconnectAsync` 和
`DisposeAsync`。当前不提供额外的通用 Host 层，避免在没有批量设备启动需求时引入中间转发：

```csharp
await using var printer = new PrinterFactory().Create(printerOptions);
var connected = await printer.ConnectAsync();
if (!connected.Success)
    throw new InvalidOperationException(connected.Message);

await printer.PrintRawAsync(printData);
await printer.DisconnectAsync();
```

如果某个应用将来确实需要统一启动大量设备，应在该应用中根据实际启动顺序、回滚策略和
故障隔离要求实现编排，而不是把所有设备强制套入一个万能生命周期容器。
