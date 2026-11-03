# Vision.FlowNode — 新引擎契约首批迁移

用户明确确认破坏性迁移后，本项目退役旧26个会话/绑定源码文件；不恢复 `Tool/Session/Provider`、旧结果、通用兼容节点或旧节点身份。
旧源码、README、项目文件的分层快照及SHA256清单位于 `../Tests/obj/flow-node-before-engine-migration-hierarchy.zip`、`../Tests/obj/flow-node-before-engine-migration-manifest.json`。历史阶段证据不代表当前契约接入完成。

## 交付链及端口

| 节点 / 新稳定ID | 配置 | 输入 → 输出 |
|---|---|---|
| `ImageFileNode` / `vision.engine.image-file` | 明确绝对FilePath | 无图请求 → Result:ImageFileOutput、OutputImage:VisionFrame |
| `ThresholdRangeNode` / `vision.engine.threshold-range` | ushort Minimum/Maximum闭区间，原编码灰度值 | Image:VisionFrame → Result:BinaryImageOutput、OutputImage:VisionFrame |
| `BlobNode` / `vision.engine.blob` | MaximumResults 1..1000 | Source:BinaryImageOutput → Result:BlobOutput |
| `BlobCountNode` / `vision.engine.blob-count` | 非负整数Minimum/Maximum数量闭区间 | Source:BlobOutput → Result:BlobJudgmentOutput、Passed:bool? |

连接：文件OutputImage→阈值Image；阈值Result→Blob.Source；Blob.Result→数量Source；数量Passed可交布尔分支消费。此批不交付分支画布/编辑器UI验收。

- 文件节点默认关闭IN作为入口，其他节点保留IN；若文件节点需要控制流前驱，显式打开IN。文档保存原控制流配置。
- 阈值全图，不猜颜色转换/归一化；Blob只消费二值快照的255前景，固定八连通、保孔、面积降序，超预算失败不截断。几何/面积仍属派生图像素空间，不转换为毫米。
- 空Blob `NoMatch`保留零区域快照，流程状态仍成功，下游可判断数量0。有效NG为成功执行与Passed=false，不抛执行异常。失败、Unsupported、取消和关闭不发布旧快照；不选择或回退后端。
- 所有算法及公共判定只调用借用的 `IVisionEngine`，此项目不引用Vision.Tools、厂商SDK或Vision.Controls，不复制算法。厂商支持范围取决于引擎；当前真实四节点验证仅OpenCV。HALCON Blob仍Unsupported。

## 生命周期及失效

| 对象 | 创建/所有者 | 节点行为 |
|---|---|---|
| IVisionEngine | 外层装配 | 仅借用，不Dispose |
| VisionContext | 每个节点首次执行创建并独占 | 关闭等待正在执行的方法返回后调用ReleaseContextAsync；失败保留上下文，下次DisposeAsync重试 |
| 输入及结果 | Core不可变托管快照 | 借用/发布，不释放上游，不保存到文档 |

不在构造、读取属性、发现或恢复文档时创建上下文/读图/调用SDK。节点执行不可重入，执行期间不能改配置或输入。修改配置先撤销结果及输出端口，再发新配置通知；取消后即使引擎返回成功也不发布。关闭不可撤回，迟到结果不发布；取消等待不证明原生停止，由引擎的释放契约继续保证原生资源安全。

`ExecuteAsync(NodeExecutionContext)`严格读取本次端口值（包括null），不复用基类SyncInputValues跳过null后残留的属性。`ExecuteAsync(CancellationToken)`供显式属性注入的离线节点调用使用。两者均只有同一个IVisionEngine业务入口。

## 发现与文档

复用现有FlowEditor机制，宿主向自己的反射工厂注册借用引擎：

```csharp
var catalog = new FlowNodeCatalog([typeof(ImageFileNode).Assembly]);
var factory = new ReflectionNodeFactory(null, catalog);
factory.RegisterService<IVisionEngine>(engine);
var mapper = new FlowDocumentMapper(factory, catalog);
```

项目仍依赖WPF的FlowEditor.NodeModel及共享PropertyGrid.Core，不宣称无WPF模型。四节点以FlowTool/NodeTypeId声明发现，以NodeInput/NodeOutput声明强类型端口，沿用原自定义端口和文档端口ID机制。

`INodeDocumentState`仅保存Version=1和表中列出的配置，拒绝未知版本、缺失/额外字段及非法区间；图像、结果、上下文、引擎和SDK资源不入文档。旧身份和旧会话配方没有自动转换。恢复只验证配置，不读取文件或执行算法。

## 验证及未完成边界

`../Tests/Verify-FlowNodeEngine.ps1`完整构建FlowNode和OpenCV生产项目，再直接编译既有Workbench测试线的 `Controls/EngineVisionNodeTests.cs`；不新增测试项目、不移除或排除生产源码求绿。脚本使用已有独立验证资产；资产缺失时先对同一路径还原现有项目图。

首轮12项：11通过、1失败（新配置通知仍暴露旧结果）；修复通知顺序后12通过、0失败、0跳过。覆盖真实OpenCV离线PGM四节点模型链（2区域OK、零区域OK、2区域NG）、本次null输入、结果失效、取消/关闭迟到结果、重入、释放重试、Unsupported边界、四节点JSON配置及端口ID往返、旧状态拒绝。

构建通过和模型级测试不等于画布运行、UI交互或真实工件验收。本批仅保证节点本地输入/配置失效，全图编辑联动失效和画布调度仍须后续接入验证。本批不创建Application/窗口，不运行宿主、HALCON、新AI或设备。Controls/Workbench旧消费者及完整历史测试仍未迁移，不构建它们；不删除旧有效测试，不宣称五层已全面接通。定位、标定、其他图像处理节点和整任务资产文档不在本批范围。
