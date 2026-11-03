# Vision.Tools

统一执行器的共享实现，仅引用 Vision.Core。公开装配入口为 VisionEngineFactory，业务执行接口始终是 IVisionEngine。

## 目录

- VisionEngine.cs：引擎状态与上下文创建。
- VisionEngine.Models.cs：明确模型路由、候选所有权、准备事务与原子替换。
- VisionEngine.Execution.cs：前置校验、执行与强类型/来源/空结果一致性核验。
- VisionEngine.Location.cs：Blob与三类卡尺共用的原定位发布账本、输入快照与源模型当前修订检查；弱引用不无限保留每帧结果或整幅搜索图。
- Common/CommonVisionOperations.cs：真正无SDK的操作表、公共分派及QR文本Ordinal全文匹配/返回集成功解码数量、存在性/数量/面积/测量公差判定；通过同一引擎执行，不是第三厂商或备用算法。
- Common/CommonVisionOperations.Classification.cs：已完成分类快照的显式预测位置/Ordinal类别/分数判定，不推理、不猜业务类别，源为空/位置缺失为NG Success。
- Common/CommonVisionOperations.Geometry.cs：点点/点线距离、明确方向夹角和无限直线交点，以及公共输出的帧/空间、数值含义/状态核验。
- VisionEngine.Lifetime.cs：上下文独占、关闭等待和资源清理重试。
- IVisionBackend / IPreparedVisionModel / BackendOutput：只在实现层内部协作，不进入公开业务契约。

- Common/CommonVisionOperations.Calibration.cs：已知长度均匀比例、带标定依据的毫米长度换算与结果核验；像素/毫米公差走独立强类型请求，非SDK模型或备用厂商。
- Common/CommonVisionOperations.Affine.cs：同帧像素/毫米世界点对的归一化QR仿射拟合、条件数/残差与无映射诊断、显式双向点映射及同一标定的世界点距；不把像素宽度标量直接换毫米。

- Common/CommonVisionOperations.WorldLines.cs：完整世界直线映射及毫米欧氏点线垂距，区分扫描轴点距与法向距离，不夹取有限线段或复用像素法向。

工厂只加载冻结选择对应的模块，不扫描任意插件。未知身份、设备、模型定义和运行库错误不能触发回退。

每上下文独占当前模型；运行中只借用，取消等待不授权提前释放。迟到结果不发布，清理失败保留所有权并允许重试，原错误不被清理错误覆盖。

独立验证目录必须先用相同`--artifacts-path`对当前HALCON/AI项目图执行restore，再使用`--no-restore`构建；默认obj已还原不代表独立目录也有资产。现有Licensing依赖资产缺失已按总体计划第29节解除，不修改授权逻辑；回归需同步本次SDK模块及Extension/Core/Licensing传递DLL，不能用旧缓存冒称新图已验证。

当前支持矩阵、退役需求和本轮验证范围统一见 [Tools README](../README.md)。不将历史全量测试数字作为当前状态。
