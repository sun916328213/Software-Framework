# Mitsubishi.MxComponent.Interop

三菱 PLC **MX Component** 的 COM Interop 程序集（RCW）NuGet 包。

## 包含内容

6 个托管 Interop 程序集，全部位于 `lib/net9.0/`（与正式 Communication 的 `net9.0` TFM 对齐；运行时仍限定 Windows x64，见下）：

| 程序集 | 接口 | 位数 |
|---|---|---|
| `Interop.ActUtlType64Lib.dll` | 数据通信（ActUtlType） | 64 位 |
| `Interop.ActUtlTypeLib.dll` | 数据通信（ActUtlType） | 32 位 |
| `Interop.ActProgType64Lib.dll` | 程序下载 / 监控（ActProg） | 64 位 |
| `Interop.ActProgTypeLib.dll` | 程序下载 / 监控（ActProg） | 32 位 |
| `Interop.ActSupportMsg64Lib.dll` | 支持消息（ActSupportMsg） | 64 位 |
| `Interop.ActSupportMsgLib.dll` | 支持消息（ActSupportMsg） | 32 位 |

## 使用方式

```xml
<PackageReference Include="Mitsubishi.MxComponent.Interop" Version="5.0.0" />
```

引用后即可在代码中 `using` 对应的 Interop 命名空间，例如：

```csharp
using Interop.ActUtlTypeLib;
using Interop.ActUtlType64Lib;
```

## 运行布局

包内 `build/Mitsubishi.MxComponent.Interop.targets` 会在消费项目构建/发布时把 6 个 RCW
复制到输出目录（与旧 HintPath 引用的复制行为一致），运行时 COM 激活可直接找到它们。

## 32 位 / 64 位选择

- 64 位进程使用 `Interop.*64Lib.dll` 程序集。
- 32 位进程使用 `Interop.*Lib.dll`（无 64 后缀）程序集。
- 两个程序集可同时被引用；运行时按进程位数加载对应的本机 COM 组件。

## 重要说明

- 本包只封装托管 Interop 层（RCW），**运行机器必须安装三菱 MX Component 原生运行时**
  （如 `ActUtlType.dll`、`ActProgType.dll` 等），并正确注册 COM 组件（通常是 64 位注册到
  64 位注册表、32 位注册到 WOW64 注册表）。
- 程序集来自三菱电机，版权归原厂商所有，**仅供内部项目使用，不得对外分发牟利**。
- 包内版本号以 64 位程序集的 `v5.0.0` 为准。
