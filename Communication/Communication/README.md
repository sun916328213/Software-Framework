# Communication

仓库项目名已收敛为 Communication。
为避免本轮产生公开 API / 二进制破坏，现有 namespace、程序集名与 NuGet PackageId 仍保持 CommunicationNew。

CommunicationNew 是面向工业通信场景的 .NET 9 通信库。公共能力按连接、地址读写、原始发送、请求/回复和被动接收拆分，应用可以只依赖实际需要的接口。

## 支持范围

- Modbus TCP 客户端与从站
- Modbus RTU 客户端与从站
- 三菱 MC/SLMP，以及可选的 MX Component 后端
- OPC UA 客户端与服务端
- TCP、UDP、Serial 原始通信
- HTTP 与 WebApi 客户端和服务端

## 使用原则

通过 `CommunicationNew.Abstractions` 中的小型能力接口使用设备：

- `IConnection`：连接生命周期和状态
- `IAddressReader` / `IAddressWriter`：具有地址语义的协议
- `IRawSender` / `IRequestReplyClient`：原始通信
- `IReceiveSource<ReceivedPacket>`：有界、可取消的被动接收

旧的 `ICommunicationDevice` 仍用于兼容已有调用方。新代码应优先依赖上述实际能力，不要通过旧接口探测协议是否支持某项操作。

## 三菱 MX Component

MX Component 直接模式仅支持 Windows x64，并要求目标机器正确安装和授权 Mitsubishi MX Component。RCW 由内部 NuGet 包 `Mitsubishi.MxComponent.Interop`（5.0.0-internal.3）提供，构建/发布时自动复制到输出目录；本包不再自行打包厂商 Interop DLL。

需要隔离同步 COM 调用或提供硬超时时，可使用项目中的 net48 x64 bridge 模式。

> ⚠️ **2026-09-23 状态注**：本段描述的 **net48 x64 bridge 模式已整体删除**（`MxComponentBridgeClientAdapter` / `MxComponentBridgeOptions` / `MxBridgeExecutablePath` / `MxBridgeStartupTimeoutMs` 均不再存在），MX Component 现在**只保留 64 位 Interop 直接模式**。保留原文仅供历史追溯。

## 兼容性与架构

包构建启用了 .NET SDK Package Validation。完整的设计边界、迁移状态和验收结果见仓库中的 `ARCHITECTURE.md`。

## P11 物理四层（2026-09-28）

```text
Communication/
├─ Communication.Core      # net9.0 / AnyCPU：能力契约、参数 DTO、纯转换/帧、工厂与能力 seam
├─ Communication.Runtime   # net9.0 / AnyCPU：协议、Transport、Session/诊断、默认组合（唯一真实 IO 归属）
├─ Communication.Controls  # net9.0-windows7.0：仅依赖 Core（契约 + 参数描述）
├─ Communication.Workbench # net9.0-windows7.0：组合 Core + Runtime + Controls
├─ Communication           # 兼容门面：仍生成 CommunicationNew.dll（TypeForwardedTo），PackageId=CommunicationNew
└─ Communication.Tests     # 含程序集级四层守卫（Architecture/LayeringTests + PhysicalLayeringTests）
```

依赖方向：`Core ← Runtime / Core ← Controls，Workbench → Core + Runtime + Controls，门面 → Core + Runtime`。
命名空间保持 `CommunicationNew.*` 不变（避免全仓改 using）；程序集归属按上述切分。
`Device.Runtime` 已迁移到 `Communication.Runtime`；`FlowNodes` / `Packing Machine` 本轮走兼容门面，不迁移。
