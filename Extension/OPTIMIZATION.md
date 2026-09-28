# Extension 优化项（2026-09-28）

## P2：生产消费接口的能力边界

**Symptom**：`Patterns/Pipelines/ProducerConsumer/ProducerConsumerPattern.cs:14-34` 的 `IProducerConsumer<T>` 要求 `StartAsync`；`Patterns/Pipelines/ProducerConsumer/Advanced/AdvancedProducerConsumerPattern.cs:487,690-693` 的 `PriorityQueueProducerConsumer<T>` 实现该接口却总是同步抛 `NotImplementedException`，提示调用特有的 `StartConsumersAsync`。`Extension.Tests/PatternsProducerConsumerTests.cs:494-503` 明确把该抛异常行为写成测试。图查询到该实现的直接调用者仅为该测试；图覆盖元数据未提供可靠新鲜度，故不能将“无生产调用”当作结论。

**Source**：`Clean Architecture` 的接口隔离；`Refactoring` 的 Refused Bequest。

**Consequence**：按 `IProducerConsumer<T>` 编程的调用方无法安全替换成优先队列实现，通用接口承诺与实际能力不同。现有测试能防止行为变化，但也会把不合适的契约固定下来。

**Remedy**：先确认优先队列是否需要通用的生产者启动语义。若需要，真正实现 `StartAsync`；若不需要，定义表达“外部入队 + 启动消费者”的专用接口，并在核对调用方后让该类不再声称支持通用启动。此处属于 API 设计调整，实施时要保护现有公开使用面。

**验收**：接口级测试验证每个实现都支持自己声明的操作；迁移测试覆盖优先级、停止与取消。现有 `Extension/ARCHITECTURE.md` 的单程序集能力边界继续有效，无需为此拆分项目。
