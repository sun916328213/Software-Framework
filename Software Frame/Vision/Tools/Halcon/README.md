# Vision.Tools.Halcon

直接 SDK 实现传统视觉操作，不再提供旧 ToolBase、Session、编辑接口或旧结果转换链。

- HalconBackend：声明能力、检查参数组合，转换厂商观测为 Core 结果。
- HalconVisionRuntime：原生形状模型准备/搜索及资源管理。
- HalconVisionRuntime.Processing：处理ROI的区间阈值/统计，以及Mono8/Mono16矩形裁剪。

HALCON SDK 仍经仓库现有 Extension 引用入口获取，保持23.11版本，不新增另一份 SDK 包。

形状匹配必须明确选择 NativeAutomatic。Core 角度为屏幕顺时针；Row/Column、原生角度、模型质心偏移和半像素约定在适配边界转换。

自定义锚点搜索掩膜和部分跨原生训练边界子域尚未适配，明确拒绝；原生精化结果越界时整次失败，不发布不完整结果。

所有原生资源只有一个拥有者。临时资源清理失败会被保留，关闭可重试；取消等待不能先行释放仍被 SDK 使用的图像或模型。

处理ROI先按Core像素规则生成游程，再通过GenRegionRuns/ReduceDomain交给SDK，避免几何取整规则不同。裁剪在边界显式换算HALCON含尾端点，输出复制到独立Core图像。

T1第一批验证使用 SDK 边界 fake，未运行新的原生 HALCON 算法。支持矩阵与后续重建范围见 [Tools README](../README.md)。
