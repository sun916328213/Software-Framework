# P3 定位能力矩阵

当前证据：原结果通过IFixtureResult显式提供位置/角度/分数/尺度；面积目标通过原ISearchRegionTool声明能力；V1.42/R4-A1的OpenCV找线通过限定ILineSearchFixtureTool准备线搜索快照（不是面积域）。V1.43/R4-A2的Hough以ISearchRegionTool筛候选圆心、ISearchRegionFixtureTool同步期望点。不按Name/Backend猜算法能力，也不把Pose2D当一般仿射。

| 当前目录工具 | 定位来源 | 主链ROI跟随 | 证据/限制 |
|---|---|---|---|
| OpenCV/TemplateMatch | 支持，位置、零角度、单位尺度 | 不作为ROI跟随目标 | 真实离线匹配→Blob、UI选择来源/基准、换位置、手动执行、重开回归；算法没有旋转估计，不宣称能跟随旋转目标 |
| HALCON/ShapeMatch | 支持原候选的位置、真实角度与尺度 | 不作为ROI跟随目标 | 托管原结果契约有±90°/180°回归；非单位Scale拒绝。真实训练/匹配需Runtime/License，未验收 |
| OpenCV/GrayStatistics | 不提供定位候选 | 支持原搜索ROI的同输入刚体准备 | E1新增真实模板匹配→统计ROI平移回归，原ROI不改；复用SearchRegion的几何/旋转语义，不宣称本轮重跑所有形状×角度组合 |
| OpenCV/IntensityTransform、AdaptiveThreshold、ColorSegment | 不提供定位 | 不作为搜索ROI跟随目标 | 全图灰度变换/局部阈值/颜色范围处理；输出同尺寸同坐标，不因存在ROI标注就隐式应用搜索域 |
| OpenCV/ImageConvert、ImageFilter | 不提供定位 | 不作为搜索ROI跟随目标 | 全图显式转换/滤波，保留像素坐标/帧信息，不因存在ROI标注就暗中裁剪或套域 |
| OpenCV/Blob | 不提供定位候选 | 支持基准搜索ROI的平移/旋转 | 真实OpenCV合成图平移/正交旋转与结果像素验证；原ROI不变，本次搜索域以紫色只读虚线叠加 |
| HALCON/Blob | 不提供定位候选 | 原搜索域契约支持刚体 | 同一会话/搜索域能力；真实SDK及各形状组合仍待授权验收，不以OpenCV抵扣 |
| OpenCV/Threshold、Morphology、Crop | 不提供定位 | 明确不支持当前搜索ROI跟随 | 保留显式图像输出能力及原处理参数；裁剪/图像预处理不是位置测量，不能凭输出图猜姿态 |
| OpenCV/FindLine | 不提供定位 | R4-A1卡尺/旧搜索带主链跟随已接 | 真实TemplateMatch平移→实际找线→测长/原参数不变/重开；单工具和编程姿态会话90度旋转是真找线，不冒称旋转匹配。原定位连接/基准、独立快照及只读派生编辑框，动态断线拒绝 |
| OpenCV/FindCircle | 不提供定位 | R4-A2圆心选择域/期望点刚体跟随已接 | 真实模板正/负平移排除原位干扰圆→Hough实际直径/重开，原ROI/期望点不改；只筛候选圆心，不裁像素/填黑/约束完整圆周或当环形卡尺。90度编程姿态会话/原程序化入口真实找圆，不是旋转匹配 |
| HALCON/FindLine、Barcode、DataCode | 不提供定位 | 当前明确未接主链跟随 | 保留原算法；厂商算子可接受域不等于该工具已声明和验证域能力 |
| HALCON/NPointsCalibration、Fixture | 当前未声明候选输出 | 不适用当前图像ROI跟随 | 标定/坐标计算保留；不能把存在仿射类或旧Fixture工具当新主链已消费其结果 |
| HALCON/ImageFile | 不提供定位 | 不适用 | 取图/读取，不应强塞定位ROI |
| AI/Classification、Detection、Segmentation、AnomalyDetection | 当前未声明定位候选 | 当前未接主链跟随 | Provider/模型生命周期保留；检测框不能自动假设为具有方向/尺度的姿态 |

## 坐标与本次性

- Pose2D按原契约使用图像像素坐标，X=列、Y=行，角度为度；不把世界/机台毫米直接画在像素图上。
- 来源/目标的本次输入须有相同尺寸、format、stride、frame/timestamp和像素指纹；这是严格的同输入图像约束，不宣称已支持裁剪偏移、不同预处理图或跨图坐标链的自动换算。
- 源结果记录执行准备时的输入身份；来源结果为空/失败、候选索引越界、非有限坐标/角度、帧变化或像素被改写均拒绝。无匹配不能回退旧位姿。
- 匹配索引从0起，显式选择不同候选后须重新“设为定位基准”；基准ROI始终归原ROI编辑器，每次从基准推导，不累计改写。
- 找线/找圆另要求与来源同直接坐标锚点：两个Crop即使像素/hash相同也拒绝，同一来源才可消费；不推广为一般坐标等价。
- 找线UseLineFixtureBinding默认为false，经原选择定位源启用；原画布断线/删源仍true，下一次不能用基准或旧位姿成功。关闭但有定位线也拒绝，显式关闭且断开才无跟随；原面积工具的兼容模式不改。
- 找圆UseCircleFixtureBinding同样默认false/原选择来源启用，断线删源保true拒绝、关闭仍有线也拒绝；当前位姿/派生期望点不写新找圆文件。无域保原全图就近，有域先筛中心且无候选不回退域外圆。
- 上游结果失效及连接变化撤下下游旧结果；目标在后台调用中失效仍以原revision隔离迟到结果，搜索域叠加同步撤销。
- 当前只接受单位尺度的刚体；一般缩放/非均匀缩放/镜像未授权定义，明确不支持、不近似圆/卡尺或静默丢掉Scale。

## 尚未完成

HALCON FindLine/FindCircle、真实旋转匹配、环形卡尺/完整圆周搜索、图像偏移/预处理链和真实HALCON/AI验收仍是差距；R4-A1/A2仅完成OpenCV两种找线及原Hough候选圆心域/期望点的限定跟随，不是一般坐标链。矩阵列出“不适用/明确未实现/待授权”不等于用户第8项或整个P3已经全部交付。完整实施/测试与执行后对照见IMPLEMENTATION_PLAN和Tests/EDITOR_REFINEMENT。
