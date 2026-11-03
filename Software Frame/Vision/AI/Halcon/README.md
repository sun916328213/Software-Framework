# Vision.AI.Halcon

当前仅提供统一 IVisionEngine 下的 HALCON CPU 单标签分类实现，不保留旧 IAIProvider 或通用 AI Tool 转发层。

- HalconClassificationBackend：设备、格式、尺寸校验，模型文件SHA256与读取租约；先完整拒绝原始预测空项或Ordinal重复类别ID，再按请求做阈值/排序/数量过滤，不能让过滤掩盖坏数据。
- HalconClassificationRuntime：直接加载/执行 SDK 模型，读取原始类别ID和置信度并管理原生资源。
- IHalconClassificationRuntime：最末端 SDK 测试缝，只在实现层内部使用。

只支持显式 cpu、单通道 Mono8、模型原尺寸、模型输入范围0..255。不猜测缩放/填充/归一化，也不把 GPU 请求静默转 CPU。

分类结果进入统一Runtime发布前，须符合本次最大数量/最低分数、Ordinal唯一类别ID及分数非升序；坏结果失败无Value，不在发布层重新筛选/排序/截断。同分允许且不构成唯一最优或概率校准证明，空保留结果仍NoMatch。相关修复/6项边界测试及52项回归见总体计划第30节，不代替真实模型推理。

已完成分类快照可通过同一引擎的公共AiPredictionJudgmentOperation作显式零基位置/期望类别/最低分数判定，源模型上下文关闭后仍可使用；不是再次HALCON推理、不猜正常/缺陷类别。SDK fake闭环与9项新判定测试见总体计划第31节，真实模型前置不变。

模型准备和真实推理是不同证据。当前文件读取租约用于阻止正常写入/删除竞争，不宣称抵御恶意路径替换。

本轮只运行分类边界 fake，未加载真实 AI 模型；检测、分割、异常检测及其他设备仍需对应模型和授权。

支持矩阵和后续阶段见 [Tools README](../../Tools/README.md)。
