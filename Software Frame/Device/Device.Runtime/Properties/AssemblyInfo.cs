// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 直接导入的协作命名空间包括：System.Runtime.CompilerServices。导入不代表所有类型都会在运行时加载。
using System.Runtime.CompilerServices;

// 仅向驱动测试程序集开放内部适配器，以便通过传输 seam 验证分帧行为而不连接真实串口。
[assembly: InternalsVisibleTo("Device.Tests")]
