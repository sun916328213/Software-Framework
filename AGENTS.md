---
description: C#框架（WPF 工业自动化控制框架）项目规则。Codex 在此仓库内查询代码结构、修改代码、评估改动影响时必须遵守。
globs:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.sln"
  - "**/*.xaml"
  - "**/*.ps1"
---

# C#框架 — Codex 项目规则

## 项目概览

WPF 工业自动化控制框架。多模块解决方案（主解决方案 `Frame.sln`）：流程编辑器（FlowEditor/FlowNodes）、通信（Communication）、视觉（Vision/Extension.Vision）、SECS/GEM 通信（Secs）、运动控制（MotionControl）、设备抽象（Device）、数据管理（DataManager）、自研控件库（WPFControl/WPFCustomControls/WinFormControl）。59 个项目，全为 SDK 风格 csproj。

## Codebase-Memory 使用（图查询优先，替代整文件读取）

仓库已安装 `codebase-memory-mcp`（MCP + 会话自动注入）。Must：

1. 会话开始做结构化探查前，先 `list_projects` → `index_status` 确认本项目已索引；未索引先 `index_repository`。
2. 结构类问题用图工具，勿直接全文件 grep/Read：`search_graph`（按名找符号）、`trace_path`（调用链，先 `direction="inbound"` 找调用方、`outbound` 看依赖、`both` 看全貌）、`get_code_snippet`（读函数源码）、`get_architecture`（代码库概览）。
3. 改动前用 `detect_changes()` 做影响分析；死代码/高扇出用 `search_graph(max_degree=0 / min_degree=10)`。
4. 架构决策用 `manage_adr` 记录（action: list/create/update），替代散落的口头约定。
5. 按证据层级定深度：Scout(T1) 快速定位、Verify(T2) 默认、Auditor(T3) 全面核验。任何"无调用者/无引用/无残留"的结论，Must 先反向 trace + `check_index_coverage` 核验覆盖范围，图未覆盖的区间再用 Grep/Read 兜底。
6. 不得修改 `.codebase-memory/` 目录内容（MCP 索引/ADR 存储，由工具管理）。

## Skill 使用

本项目为 C#/WPF 桌面框架，无 Web 前端、无 git CLI。Must：

**强制使用**
- `codebase-memory`：图查询（见上节）。

**常用（对应场景自动触发）**
- `tdd`（新增/修改测试）、`code-review`（审查改动）、`diagnosing-bugs`（疑难排查）、`brooks-audit`（架构/分层审计）、`brooks-test`（测试质量评审）、`karpathy-guidelines`（编码规范）、`research`（查证）、`implement`（按规格实现）、`handoff`（会话交接）。

**低频按需**
- `brooks-debt`（技术债评估）、`brooks-health`（代码健康度）、`improve-codebase-architecture`（架构深化）、`domain-modeling`（领域建模）、`codebase-design`（模块设计）、`to-spec` / `to-tickets` / `triage` / `wayfinder`（票务化流程）、`prototype`（原型验证）、`security-best-practices` / `security-threat-model`（安全审查）。
- 写作/教学类 `stop-slop`、`edit-article`、`teach`、`grilling`（方案访谈）仅当用户明确要求文案/教学/方案讨论时使用。

**禁用（与本项目不匹配）**
- `playwright`、`playwright-interactive`（Web 浏览器自动化）、`cli-creator`、`setup-pre-commit`（JS 生态）、`spec-kit`、`gh-address-comments`、`gh-fix-ci`、`resolving-merge-conflicts`、`security-ownership-map`（依赖 git/GitHub）、`setup-matt-pocock-skills`（一次性配置）、`grill-me`、`grill-with-docs`（与 `grilling` 重复）、`sentry`（未接入 Sentry）。

## 目标框架与平台

- 主栈统一 `net9.0-windows7.0`（WPF 宿主、控件、演示程序）；纯逻辑/运行时库用 `net9.0`（不带 `-windows`，如 FlowEditor.Runtime、CommunicationNew、WPFCustomControls.PropertyGrid.Core）。
- `UseWPF=true` 或 `OutputType=WinExe` 的 WPF 项目 Must 使用 `-windows` TFM。
- 平台统一 `AnyCPU`：csproj 内 `<Platform>AnyCPU</Platform>`、sln 平台映射全部指向 `Any CPU`。不应新增 x64/x86 项目平台（SDK 风格项目需在 csproj 显式声明 `<ProjectConfigurations>` 才支持）。位宽由 `<PlatformTarget>` 决定。不得通过 `PlatformTarget=x64` 绕过 AnyCPU 约定；厂商 SDK 位数限制在运行时检查并给出诊断。
- 新增项目时：TFM 按上述规则选、平台复制现有 AnyCPU 写法、并在对应 sln 补齐三个平台映射（Debug|Any CPU / Release|Any CPU 的 ActiveCfg 与 Build.0）。

## 模块与依赖红线

- `Secs/secs4net-base` 是第三方库（secs4net）的本地化副本，**不换 NuGet 包**。其 src 多目标 `netstandard2.0;net9.0-windows` 为有意保留（net472/net48 消费方经 netstandard2.0 桥接）；`Directory.Build.props` 中 PolySharp 仅对 `< net8.0` 目标生效、测试项目钉 `Microsoft.Bcl.AsyncInterfaces 10.0.8` 均为既定修复，勿回退。
- `CommunicationNew.MxComponentBridge` 是 net48 孤岛（三菱 PLC 原生组件依赖），不得被 net9 项目引用，勿升级其 TFM。
  > ⚠️ 2026-09-23：该工程已按《Software-Framework_Communication_Device重构删除实施说明》**删除**，
  > 上述红线随之失效（原行保留备查）。MX Component 现只保留 64 位 Interop 直接模式
  > （`MxComponentMcClientAdapter`），不再有 net48 桥接进程。
- 分层红线：核心库不得引用 WPF 类型（保持纯 `net9.0`）；跨层依赖只能自上而下（宿主/演示 → 模块 → 核心库）。
- 勿改动其他 AI 工具的配置目录（`.qoder/`、`.trae/`、`.workbuddy*`、`.zcode/`、`.vscode/`、`.vs/`）。

## 构建、测试与验证

- 构建：`dotnet build <csproj|sln> -c Debug`；测试：`dotnet test`。
- 修改 csproj（TFM/平台/引用/依赖）后 Must 重新构建受影响项目及其直接消费者验证。
- 删除或覆盖既有文件前 Must 确认其为本次任务产物；用户工作区其他文件只读不改。
- 平台相关的构建验证在 `Any CPU` 下进行。

## 环境约束（Windows）

- 无 Bash、无 git CLI（PATH 不可用），命令一律用 PowerShell；勿依赖 git 子命令。
- PowerShell 5.1 执行含中文的脚本 Must 存为 UTF-8 BOM（否则中文路径/内容乱码）。
- csproj/sln 部分为 CRLF 行尾，正则处理行尾时用 `\r?$`。
- VS 临时文件 `*_wpftmp.csproj`、`obj/`、`bin/`、`.tmp-render*` 一律不修改、不纳入规则。
