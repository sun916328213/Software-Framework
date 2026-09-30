using System.Runtime.CompilerServices;

// 仅向驱动测试程序集开放内部适配器，以便通过传输 seam 验证分帧行为而不连接真实串口。
[assembly: InternalsVisibleTo("Device.Tests")]
