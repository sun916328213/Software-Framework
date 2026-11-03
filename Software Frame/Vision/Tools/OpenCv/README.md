# Vision.Tools.OpenCv

直接实现新 Core 操作，不再提供旧工具类或旧图像转换链。

- Imaging/OpenCvFrameAdapter：按明确格式逐行复制输入/输出，保留位深和通道，不读取padding，也不借用托管缓冲指针。
- Matching/TemplateMatchSearch：Mono8 有界离散姿态搜索、二值参与域、锚点搜索掩膜及跨姿态抑制；直接返回 Core 候选。
- Engine/OpenCvBackend：准备独立 Mat 模板、校验所选能力并分派操作。
- Engine/OpenCvBackend.Processing：可选处理ROI的区间阈值/总体灰度统计，以及保留Mono8/Mono16/Rgb24/Bgr24/Bgra32/Float32格式的整数矩形裁剪；不隐式变换颜色、alpha或浮点。
- Engine/OpenCvBackend.Histogram：独立GrayHistogram请求的模块内托管整数内核；不是SDK CalcHist调用。Mono8/16完整原位深long计数、明确分箱/不足宽度尾箱、分箱前排序中位数，忽略padding/ROI域外，最多2000万输入像素；已有GrayStatistics行为不变。
- Engine/OpenCvBackend.ManyQr：DetectMulti后按原始四角组逐候选Decode，明确1..32检测预算、保留未解码几何和相同文本不同位置，超限/异常不发布部分计数；原图几何排序不是场景全码或跨帧身份。
- Engine/OpenCvBackend.Qr：Mono8单目标标准QR，整数矩形窗实际裁图、原生DetectAndDecode、四角加偏移回原图；区分未检测/检测未解码/解码，独占读取器不伪造质量或场景全码计数。
- Engine/OpenCvBackend.Difference：显式当前/参考同位深Mono8/16差分，绝对/仅变亮/仅变暗与原位深正闭区间，固定域外0，带双图来源的二值候选继续接Blob/判定；不配准、归一化或偷读全局背景。
- Engine/OpenCvBackend.Hsv：Rgb24/Bgr24转普通8位HSV（H0..179、S/V0..255），跨零色相两段并集，S8=0显式无色相策略且仍受S/V约束，固定域外0；不是连续HSV或Lab色差。
- Engine/OpenCvBackend.Color：Rgb24/Bgr24的明确RGB三通道编码值闭区间分割，固定ROI域外0，独立同空间Mono8二值图继续接Blob/判定；不隐式灰度、alpha丢弃或冒称HSV/Lab。
- Engine/OpenCvBackend.Io：文件内容快照解码、显式格式转换及灰度gain/offset变换；ColorChannelExtract明确按RGB语义选择Rgb24/Bgr24/Bgra32存储下标，实际ExtractChannel输出独立同尺寸/空间Mono8图，忽略alpha，不计算亮度或归一化，不猜灰度/浮点通道。
- Engine/OpenCvBackend.Preprocessing：均值/高斯/中值滤波、原位深Otsu/反向、Mono8局部阈值和二值形态学。
- Engine/OpenCvBackend.Gamma：选中模块内托管完整灰度LUT（非SDK Gamma指令），按M×(pixel/M)^Gamma与floor(value+0.5)处理Mono8/16、忽略padding，固定0/M端点、同位深新帧、最多2000万像素；不逐图归一化，不改变GrayLinear既有舍入规则。
- Engine/OpenCvBackend.Normalize：托管两遍扫描Mono8/16全图极值并拉伸到同位深满量程，整数半向上舍入；常量图不变，返回输入极值，忽略padding，最多2000万像素。
- Engine/OpenCvBackend.Blob：原位深区间阈值、ROI、四/八连通、保孔/填孔、区域特征/轮廓和过滤排序；使用SAUF的行优先编号作为最终并列排序键。轮廓净面积使用ContourArea，含孔洞总周长使用ArcLength(closed=true)，圆度只使用同一轮廓面积口径；退化圆度明确无定义。

- Engine/OpenCvBackend.Calipers：有向路径卡尺、浮点双线性采样、切向均值、扫描轴高斯/差分选边，以及Huber初始化、残差剔除与L2直线重拟合；诊断与残差均为独立Core值。

- Engine/OpenCvBackend.EdgePairs：复用唯一卡尺剖面内核，按同一剖面内相邻合格异极性峰测量条带扫描轴宽度；先极性/宽度过滤，再明确选对，输出全部卡尺诊断与宽度统计。测宽保留紧邻异号峰，找线沿用原平顶规则。

- Engine/OpenCvBackend.Circles：复用剖面内核进行内/外向圆卡尺采样，以有界三点共识种子、归一化SVD和径向几何最小二乘拟合圆，残差剔除/有限回纳与覆盖角守卫；不以最小包围圆冒充拟合。

算法只使用当前选中的 OpenCV；不调用 HALCON，不把灰度相关分数冒充形状分数。SDK 和 Windows Runtime 包保持4.11.0.20250507。

处理ROI按Core像素中心规则栅格化。灰度/RGB阈值域外清零，统计域外忽略；裁剪生成独立帧/空间及到直接源图的映射，不保留局部Mat视图。

角度/缩放采样、数量、响应像素均有预算。不支持原生自动搜索、忽略极性或隐式连续精化。模板参与域至少4像素且需要有效灰度变化。

模板原生资源由上下文拥有，输入和临时 Mat 由本次调用拥有。结果使用 Core 图像坐标和独立托管值。

邻域操作不默认处理ROI。中值固定复制边界，16位核限3/5；其他参数组合和形态学补零语义均显式校验，不静默忽略。

三类卡尺收到的是Core冻结后的当前像素名义几何；显式定位绑定/模型修订及实际输入对象由统一Runtime在执行前和发布前核验。没有在SDK内部偷读编辑器定位或全局模型，采样与判断限不自动随缩放改变。

完整支持矩阵、未完成项和验证范围见 [Tools README](../README.md)。
