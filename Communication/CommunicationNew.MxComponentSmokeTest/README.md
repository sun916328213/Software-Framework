# MX Component 实机验收工具

该 x64 工具通过 `CommunicationNew` 的公开 `McClientDevice` interface 验证 MX Component，而不是直接绕过主库调用厂商 COM。

默认命令只读取逻辑站 0 的 `D100`：

```powershell
dotnet run --project CommunicationNew.MxComponentSmokeTest -c Release -- --station 0 --address D100 --count 1
```

桥接模式：

```powershell
dotnet run --project CommunicationNew.MxComponentSmokeTest -c Release -- --station 0 --address D100 --count 1 --bridge "C:\path\CommunicationNew.MxComponentBridge.exe"
```

密码只从环境变量读取：

```powershell
$env:COMMUNICATIONNEW_MX_PASSWORD = "your-password"
```

写入默认关闭。只有同时提供写入数据和 `--confirm-write` 才会写 PLC，随后会自动回读。写入点数由值列表决定，不能再指定 `--count`：

```powershell
dotnet run --project CommunicationNew.MxComponentSmokeTest -c Release -- --station 0 --address D100 --write-words 0x1234,5678 --confirm-write
dotnet run --project CommunicationNew.MxComponentSmokeTest -c Release -- --station 0 --address M100 --write-bits 1,0,1 --confirm-write
```

请只对允许修改的测试寄存器执行写入验收。

工具会在创建 MX COM 对象和连接 PLC 前校验参数：字设备单次最多 960 点，位设备单次最多 7168 点，并确保访问末地址没有超出 MC 的 24 位设备编号范围。

命令行解析、连接前边界和写保护由 `CommunicationNew.Tests` 的 `Protocols/Mitsubishi/SmokeOptionsTests.cs` 覆盖。参数解析类型 `SmokeOptions` 本身位于 `CommunicationNew.Protocols.Mitsubishi`（库内 `internal`，通过 `InternalsVisibleTo` 对本工具程序集开放），因此这些测试不需要引用本工具工程，也不连接 MX Component 或 PLC：

```powershell
dotnet test CommunicationNew.Tests -c Release
```
