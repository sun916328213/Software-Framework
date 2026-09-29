# Software-Framework 架构规则

> 统一规则（2026-09-29）：以 Core / Runtime / WPF UI / Host / Tests 分层为准。
> 结论先行：不追求“每个项目都一个 Tests”，而是按运行环境和职责边界拆测试。

## 1. 分层与依赖方向

```text
Core <- Runtime <- Controls <- Host / Workbench
 ^        ^          ^
 |        |          |
 +--- Xxx.Tests -----+   Host/Workbench.Tests
```

- **Core**：接口、契约、基础模型、领域结果与纯逻辑，不依赖 WPF，不引用
  Runtime / Controls / Host。
- **Runtime**：运行时编排、Manager、生命周期、轮询、驱动适配与业务执行，
  只依赖 Core，禁止反向依赖 UI。
- **Controls / WPFControl**：可复用 WPF UI 组件，只做表现层与 UI 交互所需逻辑。
  只依赖 Core 契约（不引用 Runtime 实现，避免 Runtime 正常运行以 UI 为前提）。
- **Host / Workbench**：Composition Root，集中处理 DI、配置、主题、资源、
  窗口与生命周期启动，是装配和验证入口，不承载业务逻辑。
- **Xxx.Tests**：Core + Runtime 共用的业务逻辑测试集合，不启用 WPF，
  不创建 Application，直接引用 Core / Runtime（禁止经顶层聚合项目间接获得依赖）。
- **Xxx.Host.Tests / Xxx.Workbench.Tests**：WPF 独立集成测试，
  覆盖控件实例化、Binding、资源、Dispatcher、DI 装配与宿主启动/关闭。

## 2. 测试拆分规则

- `Xxx.Tests` 内部按 `Core / Runtime / Infrastructure` 目录归类，
  不为目录边界继续拆多个 csproj。
- 只有当 Controls 自身存在大量可独立验证、且不需要真实 Host 装配的 UI 行为时，
  才额外增加 `Xxx.Controls.Tests`；否则一律用 Host/Workbench.Tests。
- 真实设备 / 长时间运行测试用 Category 或独立 Smoke 项目，
  不混入快速单元测试。

## 3. 各模块状态（2026-09-29）

| 模块 | 生产分层 | 逻辑测试 | WPF 集成测试 | 动作 |
| --- | --- | --- | --- | --- |
| MotionControl | Core/Runtime/Controls/Workbench | MotionControl.Tests（直引 Core+Runtime） | MotionControl.Workbench.Tests（P1 新建） | Tests 按 Core/Runtime/Infrastructure 归类 |
| Communication | Core/Runtime/Controls/Workbench | Communication.Tests（直引 Core+Runtime，无 UI） | Communication.Workbench.Tests（P2 新建） | 硬件测试保持 Category 隔离 |
| DataManager | Core/Runtime/Controls/Workbench | DataManager.Tests（直引 Core+Runtime） | DataManager.Workbench.Tests | 模板：只做边界审计，Controls 业务逻辑下沉 Runtime |
| FlowEditor | Core/Runtime/Controls + 专项 | FlowEditor.Tests | WPFControl.Host.Tests | 已知偏离：FlowEditor.Tests 暂混有 WPF 测试（UseWPF + 引用 Controls），保留不强拆；新增 WPF 测试一律进 Host.Tests |

## 4. CI 分组建议

- `Xxx.Tests`：普通 `dotnet test`，每次提交/PR，快速反馈。
- `Host/Workbench.Tests`：Windows + WPF，PR + 主分支，验证 UI 集成与宿主装配。
- Smoke / Hardware：专用机器或人工，按需/夜间。
