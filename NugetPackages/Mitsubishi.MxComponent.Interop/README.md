# Mitsubishi.MxComponent.Interop

三菱 PLC **MX Component** 的 COM Interop 程序集（RCW）内部 NuGet 包。
本包为 Software-Framework 内部封装，**不代表 Mitsubishi 官方 NuGet**。

## 版本语义

- NuGet `Version`（如 `5.0.0-internal.3`）中，`5.0.0` 是**厂商来源版本**
  （MX Component 产品/RCW 来源版本，另见 `MxComponentVendorVersion` 与
  `vendor-manifest.json`）；`internal.3` 是**第三次内部包装修订**。
- 不要把内部包装版本误读为三菱厂商发布版本。

## 包含内容

6 个托管 Interop 程序集，全部位于 `lib/net9.0/`（标准 lib 引用，无自定义 `build/*.targets`）：

| 程序集 | 接口 | 位数 |
|---|---|---|
| `Interop.ActUtlType64Lib.dll` | 数据通信（ActUtlType） | 64 位 |
| `Interop.ActUtlTypeLib.dll` | 数据通信（ActUtlType） | 32 位 |
| `Interop.ActProgType64Lib.dll` | 程序下载 / 监控（ActProg） | 64 位 |
| `Interop.ActProgTypeLib.dll` | 程序下载 / 监控（ActProg） | 32 位 |
| `Interop.ActSupportMsg64Lib.dll` | 支持消息（ActSupportMsg） | 64 位 |
| `Interop.ActSupportMsgLib.dll` | 支持消息（ActSupportMsg） | 32 位 |

`lib/net9.0/` 下的程序集同时作为编译引用与运行时资产：消费方通过 `PackageReference`
引用后自动获得编译解析，构建/发布时自动复制到输出目录。

## 使用方式

```xml
<PackageReference Include="Mitsubishi.MxComponent.Interop" Version="5.0.0-internal.3" />
```

引用后即可在代码中 `using` 对应的 Interop 命名空间，例如：

```csharp
using ActUtlType64Lib;
using ActSupportMsg64Lib;
```

当前 `Communication` 的 MX adapter 仅使用其中 64 位类型
（`ActUtlType64Lib` / `ActSupportMsg64Lib`）；其余 4 个作为通用内部二进制容器保留，
供仓库内其他项目按需使用。

## 打包说明（维护者）

- 本工程是纯二进制包装项目：`IncludeBuildOutput=false`，不产出自己的程序集。
- 打包前先校验厂商二进制身份（文件名 + SHA-256 + 文件版本）：

  ```powershell
  powershell -ExecutionPolicy Bypass -File NugetPackages/Mitsubishi.MxComponent.Interop/verify-vendor-manifest.ps1
  ```

  校验依据为受版本控制的 `vendor-manifest.json`（不保存厂商二进制本身）；
  校验失败则拒绝打包，避免悄悄产出同版本但内容不同的 nupkg。
- 打包使用显式 `restore + pack --no-build`，输出到本地临时验收目录：

  ```powershell
  dotnet restore NugetPackages/Mitsubishi.MxComponent.Interop/Mitsubishi.MxComponent.Interop.csproj
  dotnet pack NugetPackages/Mitsubishi.MxComponent.Interop/Mitsubishi.MxComponent.Interop.csproj -c Release --no-build -o NugetPackages/.feed
  ```

- `NugetPackages/.feed` 仅用于本机打包验证（git ignore），不是正式恢复链路；
  正式包发布到私有远程 Feed（见根 `NuGet.Config`）。

## 平台与运行条件

- 打包项目与消费项目统一 `AnyCPU`；本包不设置 `PlatformTarget=x64/x86`，
  不新增 `x64/x86` solution platform。
- 当前 `Communication` 的 MX adapter 仅在 **Windows x64 进程**中可用；
  非 Windows / 非 X64 进程由运行时守卫（`MxComponentMcClientAdapter` 检查
  `OperatingSystem.IsWindows()` 与 `RuntimeInformation.ProcessArchitecture == X64`）
  明确拒绝并给出可诊断错误，不通过 csproj 固定架构。

## 重要说明

- 本包只封装托管 Interop 层（RCW），**目标机器仍需安装/授权三菱 MX Component
  原生运行时**（如 `ActUtlType.dll`、`ActProgType.dll` 等），并正确注册 COM 组件
  （通常是 64 位注册到 64 位注册表、32 位注册到 WOW64 注册表）。
- 程序集来自三菱电机，版权归原厂商所有，**仅供内部/私有 NuGet 源使用**。
  厂商 RCW 的再分发权限须由实际许可证确认；未确认前不要公开发布到 nuget.org。
