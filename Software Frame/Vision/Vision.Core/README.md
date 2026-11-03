# Vision.Core

视觉领域的中立契约与基础类型。

本项目只定义请求、结果、图像快照、模型标识和几何关系，
不实现厂商算法，不加载运行库，也不承担配置、界面或文件存储。

## 阅读顺序

1. `IVisionEngine.cs`：统一业务入口及上下文生命周期。
2. `Engine/`：实现选择、上下文标识和操作基类。
3. `Capabilities/`：执行域、操作能力和引擎描述。
4. `Imaging/`、`Models/`：图像、掩膜和模型定义。
5. `Operations/`、`Results/`：强类型请求及本次执行结果。
6. [契约说明](CONTRACT_RULES.md)：帧身份、坐标、采样和取消语义。

## 目录职责

| 目录 | 职责 |
|---|---|
| Engine | 冻结的实现选择、工具上下文及操作基类 |
| Capabilities | 执行域、可选能力和只读能力描述 |
| Diagnostics | 稳定诊断代码和说明 |
| Imaging | 像素格式、独立图像快照和二值掩膜 |
| Models | 模型种类、模型定义、准备范围和模型引用 |
| Operations | 模板/分类、固定ROI的8位RGB/量化HSV分割、显式对齐双图差分、单/多QR、精确文本/解码数量判定、ROI阈值/统计、裁剪、文件/格式/灰度变换、滤波、Otsu/局部阈值、形态学、Blob、卡尺直线/边缘对/圆、无图几何及数量/面积/测量公差判定 |
| Results | 执行状态、候选、分类、二值图、实际Otsu阈值、统计、来源映射、图像变换、区域、卡尺样本/拟合残差及OK/NG判定结果 |
| Geometry | 点、尺寸、矩形、位姿、轮廓及带帧/像素空间的PixelPoint/PixelLine |
| Coordinate | 坐标空间、仿射变换及坐标链 |
| Calibration | 独立托管均匀比例、二维仿射点对标定与显式逆映射/残差/条件数，不加载SDK或保存文件 |
| Roi | 感兴趣区域的几何表达和定位跟随 |
| Regions | 算法输出区域与轮廓的只读描述 |

## 设计边界

- 业务执行统一使用 `IVisionEngine`。
- 请求不携带后端切换指令，也不使用任意对象参数字典。
- 实现选择在装配时冻结，失败不能静默切换厂商或设备。
- 图像和掩膜不暴露厂商对象、原生句柄或可写像素数组。
- 模型引用不代表资源一定存活，实际归属由实现核验。
- 无模型操作不准备假模型，也不撤销上下文当前模型。
- VisionOperation<TResult> 不强制携带图像；VisionImageOperation<TResult> 强制提供真实非空输入。BlobJudgmentOperation提供真正无图的存在性、数量及指定区域面积判定。
- VisionOutcome.ExecutionDuration是取得上下文独占权后、本次执行返回前的实测单调耗时；包含校验、执行和发布前检查，不含上下文获取、模型准备或调用方处理。手工构造可为null；取消异常不返回结果。
- GrayStatisticsOutput保留输入Mono8/Mono16位深；GrayStatisticsRangeOperation显式选择统计指标和闭区间，判定结果保留来源，不把灰度当成几何长度。
- GrayLinearOperation默认保留后端最近整数舍入，也可显式选择非负半向上；不能将两种口径混称等价。
- GrayGammaOperation是独立全图Mono8/16满量程幂函数请求，Gamma为有限正指数，输出同位深新帧；floor(value+0.5)仅适用于该非负Gamma结果，不改变GrayLinear的SDK最近整数规则，不按本幅图极值归一化。
- ColorChannelExtractOperation明确选择RgbChannel.Red/Green/Blue原编码分量，仅用于8位RGB/BGR/BGRA；不是亮度转换，BGRA明确忽略alpha、不与背景合成。复用ImageTransformOutput独立Mono8图，保留来源帧及像素空间，不暴露原生通道对象。
- 处理 ROI 使用冻结的 SearchRegion；阈值域外清零、统计域外不参与，空参与域拒绝。
- GrayHistogramOperation/Output是独立原位深灰度直方图/中位数契约，不改变GrayStatistics。Mono8/16箱宽为正整数编码值，箱i覆盖[i×宽,min((i+1)×宽−1,编码上限)]；Bins为只读long计数，PixelCount为其和，Median在合箱前计算，偶数样本取中间两原值均值。箱中心不能代替原始中位数；格式、箱宽、计数和帧/空间需发布核验。
- BlobOperation接收固定或跟随的VisionRegionBinding。跟随基准是模板局部坐标，必须明确候选下标；原始几何不累积修改。引擎核验实际发布对象、定位输入快照及模型当前修订，GUID相同不证明图像分支等价。
- BlobItem.Area为前景像素计数，ContourMetrics单独保存像素中心轮廓的净面积、含孔洞总周长及圆度；不混用两种面积。退化轮廓圆度为null，不满足任何圆度筛选。Index为本次一基排序位置，不是跨帧身份。Blob空集合为NoMatch，数量/面积NG仍为Success。
- 裁剪创建独立帧和坐标空间，并给出到直接输入图像的映射，不猜测祖先或世界坐标。
- ImageFileOperation从绝对路径读取文件主图，由实现层执行解码；Core不读文件。结果注明实际编码内容SHA256，并创建独立帧/空间。
- ImageConvertOperation显式指定格式，不猜灰度范围；GrayLinearOperation明确gain/offset、输出位深和限幅语义。变换结果保留尺寸/空间，但创建新帧。
- 邻域操作明确核、边界和位深；Otsu返回实际阈值，局部阈值不拿全局结果冒充，二值形态学不隐式阈值化灰度图。
- CaliperLineOperation冻结固定像素路径及卡尺参数，不引用可编辑区域。CaliperLineOutput包含本次全部卡尺的选点、参与标记和正交残差；未拟合为NoMatch及空Line，不冒充旧直线。边缘对由独立CaliperEdgePairOperation在同一剖面内实际配对，不用两条直线距离替代；圆由独立CaliperCircleOperation提供径向卡尺与圆拟合；可显式绑定TemplateLocationBinding跟随本次模板定位，可继续接T4均匀比例毫米换算，或提取明确边缘点做二维仿射映射/毫米点距；不将非等比/剪切下的像素宽度标量改标签，也未接畸变标定。
- CaliperEdgePairOutput保留每把卡尺的两侧边缘/对比度/扫描轴宽度和峰对数量；有效完整对不足时统计为空但诊断保留。平均宽度、极值和总体标准差来自所有完整选中对，均值可继续做像素公差判定。
- CaliperCircleOutput提供圆心/半径、径向参与/剔除残差及实际覆盖角；完整圆不重复首尾，局部弧包含两端。有效拟合直径以Pixels标量接入公差，NoMatch不带旧圆。
- 三类卡尺可附TemplateLocationBinding，显式选择原定位候选；保留模板局部Reference基准，实际名义几何仅变换一次。采样/残差/宽度范围等仍为当前图像像素，模型过期或换图则拒绝。
- PixelPoint/PixelLine明确记录帧与像素空间，几何运算拒绝跨帧/空间盲用。点点距离、无限直线点距、明确方向夹角及交点由Tools公共实现执行，不是边缘对测宽。
- ScalarMeasurementOutput明确几何含义及像素/角度单位；MeasurementRangeOperation要求显式匹配单位，包含上下限，NG仍Success。不直接接受毫米标签或隐式标定。
- PixelScaleCalibrationOperation由同帧同空间参考点及已知毫米长度建立UniformPixelScaleCalibration；LengthToMillimetersOperation只换算同像素空间的长度，保留原值/单位与标定身份。MillimeterRangeOperation显式接收带标定的毫米长度，不是改标签。
- 均匀比例只适用于已确认等比例像素、同物理平面及光学条件的空间，不提供世界原点、二维仿射/透视/畸变校正或精度承诺。
- AffineCalibrationOperation用3..4096个明确像素/毫米世界点对拟合二维仿射，给出正/逆系数、同序毫米残差、RMS/最大残差及条件数。病态/奇异/超残差为NoMatch诊断且Calibration为空，不保留旧矩阵。
- PixelToMillimeterPointOperation与MillimeterPointToPixelOperation明确空间、方向及逆映射目标帧。CalibratedPointDistanceOperation只对同帧、同像素空间及同一标定的两个映射后世界点求毫米距离，经IMillimeterLengthMeasurement接同一毫米公差；点距不冒充世界法向厚度、仿射圆直径或真实精度验收。
- PixelLineToMillimetersOperation完整映射像素无限直线的两个定义点；CalibratedPointLineDistanceOperation在毫米欧氏几何重算法向，输出带点/线/同一标定依据的无向垂距并接毫米公差，不夹取有限线段。局部点线距离不单凭自身证明两条边界处处平行或等厚。
- RgbColorSegmentOperation以RgbColorRange的R/G/B各0..255编码值闭区间同时筛选，产出同尺寸/像素空间的新BinaryImageOutput；固定域外清零，可接显式255前景Blob与无图计数/面积判定。仅声明Rgb24/Bgr24，不暗做灰度、忽略alpha或泛称HSV/Lab/色差；未接跨灰度定位分支跟随。
- AlignedImageDifferenceOperation显式包含Current和Reference，要求同尺寸/格式/像素空间；绝对/仅变亮/仅变暗用原Mono8/16编码值正闭区间阈值，输出带双图来源/方向/阈值的ImageDifferenceOutput。全黑Success，显式255前景送Blob再判定；同空间标签不是内容对齐证明，不自动配准/补光或偷读背景。
- QrCodeReadOperation限定Mono8单目标标准QR与整数矩形搜索窗，QrCodeReadOutput区分未检测/检测未解码/已解码，四角回到原图像素中心坐标；0/1数量不是多码场景总数。QrTextEqualsOperation无图Ordinal全文判定，不读码或不相等均为NG Success，不暗做trim/规范化或伪造质量等级。
- QrCodeReadManyOperation显式1..32候选预算，保留每个候选独立解码/未解码状态，不按相同文本去重；超限失败不截断。QrCodeReadManyOutput按几何键排序，数量只针对本次返回集；QrDecodedCountOperation无图判成功解码数量，不把检测框算成功或宣称场景无漏码。
- HsvColorSegmentOperation显式从Rgb24/Bgr24作8位HSV分割，HsvColorRange的H编码0..179（约2°/档）、S/V0..255；H下限>上限为跨179→0。S8=0无有效色相，默认排除，IncludeAchromatic仅在S/V也满足时允许忽略H；不冒称连续角度或感知色差。
- AiClassificationOutput在引擎发布时核验请求内数量/最低分数、Ordinal唯一类别ID及分数非升序，坏结果Failed无Value，不重排/截断求绿；同分合法且不宣称唯一最优或校准概率，空预测仍NoMatch。此为发布边界，不是新增真实模型/预处理验收。
- AiPredictionJudgmentOperation真正无图/无SDK，调用方显式提供分类快照、零基预测下标、期望类别ID及最低分数；Ordinal类别相等且含边界达分才OK，缺项或不符为NG Success。不会搜索别的预测、改变类别语义或证明真实推理来源。
- 失败、取消和超时不能携带之前的结果。
- 取消等待不等于实际调用停止，释放必须等待资源真正空闲。

## 代码约定

- 使用清晰的中文说明，解释用途、单位、边界及所有权。
- 控制语句使用完整代码块。
- 独立赋值和调用分行书写。
- 构造参数较多时分行排列。
- 枚举成员逐项声明并说明含义。
- 代码风格由本目录的 `.editorconfig` 约束。

## 构建

项目使用 .NET 9 和 C# 13，不引用其他工程或视觉 SDK。
System.Text.Json 仅用于领域类型的序列化声明，不承担文件读写。

```powershell
dotnet build "Software Frame/Vision/Vision.Core/Vision.Core.csproj"
```

Core 已不保留旧工具会话、旧 Provider、工具管理器及存储辅助类型。
仍引用这些类型的外层项目需要独立迁移，不能通过恢复旧接口掩盖缺失。
本层独立构建通过不等于外层迁移或实际算法验收完成。
