# Core契约语义

本页说明统一执行入口中的身份、资源和结果约定。

## 1. 唯一入口与选择

- 新业务入口只有IVisionEngine；强类型DTO、几何值和纯几何接口不是第二个执行器。
- VisionOperation<TResult>允许无图请求，Image为null；VisionImageOperation<TResult>强制真实图像输入。Blob数量/面积、点线几何与测量公差由Tools公共实现计算，不以假帧适配SDK。
- VisionEngineSelection只能指定一个传统视觉实现及可选一个AI实现；AI启用时必须有明确设备。
- Core不硬编码已部署厂商表，不读取配置、不探测License/SDK/GPU，不选择默认或回退实现。
- 选择由外层装配冻结；切换须真正停止、释放并重新装配，不是改请求字段或热改全局变量。
- VisionEngineDescriptor是操作/功能位快照，不是格式×度量×范围的全组合承诺。ValidateAsync还须核对所选实现和具体请求。

## 2. 上下文与模型

- 调用者拥有工具上下文，编辑器只能借用。每上下文有一个当前模型，不把一个原生模型资源交给多个上下文。
- 模型定义为中立值快照；AI位置+SHA256不是在Core加载文件，也不是安全凭据。
- 准备成功才发布新引用并使旧引用失效；失败清候选、保原模型。
- VisionModelReference公开的身份构造不证明模型实际存活；实现必须验证真实注册、归属、当前引用与修订。
- 无模型操作Model为null，不准备假模型，也不借用/撤销上下文当前模型。
- GrayGamma显式采用满量程M×(pixel/M)^Gamma，Gamma有限且正，不取倒数；非负结果floor(value+0.5)量化、0/M端点固定，保持原位深。不逐图拉伸，不与GrayLinear既有SDK舍入混称同一种量化。
- ColorChannelExtract按RgbChannel语义取8位R/G/B原值，不按调用者猜测的缓冲下标执行。Rgb24与Bgr24/Bgra32的R/B位置不同；BGRA忽略alpha而非与背景合成。结果为独立同尺寸/空间Mono8帧，源FrameId不改成派生图身份，不将通道原值冒充亮度。
- 灰度直方图独立于GrayStatistics默认请求：Bins是原编码灰度整数数量而非概率密度；每箱按BinWidth覆盖闭整数范围，尾箱截于编码上限。Median属于未合箱的原灰度顺序样本，偶数取中间两值均值，不以箱中心近似。空参与域不是有效零样本中位数；全黑参与域仍Success、Median=0。
- 模型引用/上下文是进程内令牌，不作为工程文件中可恢复的原生资源序列化。

## 3. 图像身份与坐标

| 类型/字段 | 现有含义 |
|---|---|
| VisionFrame.FrameId | 调用方显式提供的输入帧身份；不是文件路径、缓冲地址或采集计数 |
| VisionFrame.CoordinateSpaceId | 当前像素坐标空间身份，不自动推导世界坐标或裁剪链 |
| 模板/分类/统计结果.FrameId | 本次实际搜索/推理/统计的输入帧，不是模型或模板身份 |
| BinaryImageOutput.FrameId | 产生二值图的输入帧 |
| BinaryImageOutput.Image.FrameId | 派生二值图的独立身份，必须不同于输入帧 |
| BinaryImageOutput.Image.CoordinateSpaceId | 本操作不改坐标；实现应核验原尺寸/空间 |
| ImageCropOutput.FrameId / SourceCoordinateSpaceId | 本次直接输入帧及其坐标空间 |
| ImageCropOutput.Image | 独立帧、独立坐标空间的裁剪图，像素坐标从0开始 |
| ImageCropOutput.ImageToSource | 裁剪图到直接输入图像的平移，嵌套裁剪需显式逐级组合 |
| ImageFileOutput.Image | 本次解码创建的新帧/空间，没有假输入帧 |
| ImageFileOutput.FilePath / ContentSha256 | 本次编码字节快照的来源路径/指纹，不是模型句柄或帧等价凭据 |
| ImageTransformOutput.FrameId / Image | 直接输入帧与独立输出帧，变换/滤波保留尺寸和坐标空间 |
| OtsuThresholdOutput.Threshold / Image | 本次原位深选出的阈值及独立Mono8二值图，来源仍为直接输入帧 |
| BlobOutput.FrameId / CoordinateSpaceId | 本次连通域输入图像的帧及像素空间；不是模板身份 |
| BlobJudgmentOutput.FrameId / CoordinateSpaceId | 被判定区域快照的来源，不构造假输入图像 |

FrameId不能单独证明两个处理分支等价。
相同像素、内容指纹或坐标数值，也不能授权跨裁剪复用定位。
Core不自动推断处理来源或坐标空间之间的等价关系。

VisionFrame复制有效缓冲并只读暴露，正stride包含行填充；Mono16按PixelFormat的小端约定。VisionMask为与所属图像同尺寸的二值参与域，0忽略、1/255参与，拒绝全空/权重。

处理ROI使用SearchRegion冻结几何，并按其像素中心/半开矩形规则确定参与像素。阈值域外固定清零，统计域外忽略，空参与域拒绝而不是NoMatch。ProcessingRegion能力与模板掩膜、锚点搜索掩膜分开。

ImageCropOperation只接受完整落在图内的整数半开矩形，不静默裁切或缩放。当前实现仅接Mono8/Mono16，保留位深；结果映射不包含标定或跨分支等价推断。

文件输入需要明确绝对路径，由实现层读取和解码文件主图，不自动按EXIF旋转、遍历动画/多页或恢复标定。格式转换必须显式请求；整数存储扩大不等于灰度归一化。GrayLinearOperation使用调用方给定gain/offset并按目标位深限幅，最近整数舍入由当前实现说明，不保证跨SDK逐字节等同。

基础滤波、Otsu、局部阈值和形态学当前契约为全图，不接受隐式ROI拼接。邻域核、锚点、边界、次数和位深须明确；不支持的组合在SDK处理前拒绝。局部阈值Offset为整数，邻域统计按整数灰度实现舍入。二值形态学仅处理0/255，不自动对灰度图阈值化；开/闭次数指各基础阶段次数。合法全黑二值输出仍是Success。

## 4. 匹配与采样

- X向右、Y向下；正角度显示顺时针。锚点/角度/正等比缩放唯一决定TemplateToImage，SDK在Tools边界转换。
- 模板帧/模板参与掩膜/锚点/准备范围是一体定义，不能用搜索域替代模板掩膜。
- SearchMask限制模型锚点允许落点（最近像素中心），不是搜索图像遮挡掩膜。
- 普通TemplateSearchSampling为显式离散步长；NativeAutomatic明确委托原生搜索/精化。两种语义不等价，不支持时拒绝。
- 原生模式中AngleStepDegrees/ScaleStep的默认数值不是实际SDK采样或精度，不能当已生效参数展示。
- 搜索范围不得超出模型准备范围；Core圆周范围合法不等于某后端已实现所有跨边界搜索。
- TemplateSearchRange.Contains对角度边界使用1e-12度的数值容差，仅吸收圆周运算的舍入误差，不代表算法采样步长或测量精度；缩放边界仍严格比较。
- 得分范围[0,1]并说明ScoreMeaning，不保证厂商阈值/极性/重叠语义可直接互换。

### 定位跟随与区域判定

- VisionRegionBinding的固定域为输入像素坐标；Follow的基准域为模板局部坐标，以明确的零基CandidateIndex取得TemplateToImage，一次变换，不累积改写基准。
- Resolve只执行纯几何换算，不能证明实际结果发布、图像分支或模型有效；执行器还须核验本引擎发布的原结果对象、原模型引用、原始输入图像快照及当前修订。跨上下文使用时，执行前和发布前都检查源模型。
- 不接受重造的定位DTO或借相同GUID拼接的另一图像对象。裁剪/转换创建的新帧不能暗中复用此定位；一般跨空间/派生图映射尚未接通。
- Blob在Mono8/Mono16原数值域做包含上下限的阈值化，参与域在连通域前生效。前景四/八连通明确选择，填孔使用互补背景连通性，参与域外始终不计入。
- 面积为前景像素计数，以每像素单位面积计；质心是参与像素中心坐标的算术平均，外接框为整数半开边界。Extent=像素面积/(框宽×框高)。轮廓是像素中心边界，不将其多边形面积当作像素面积；点/线退化轮廓仍可读取。
- 特征包含像素面积、框宽高、Extent、孔洞数及单独命名的ContourArea/ContourPerimeter/ContourCircularity。范围包含上下限，支持All/Any，空筛选集不过滤。
- BlobContourMetrics.Area按像素中心闭合轮廓的格林公式面积计算，外边界绝对面积减去孔洞绝对面积；Perimeter为所有外边界与孔洞的闭合折线长度之和。不是像素单元边缘的四邻域周长，也不是亚像素/毫米测量。ROI截断产生的组件边界也计入，不推断图外目标；自触轮廓按同一公式描述，不推断真实工件填充面积。
- ContourCircularity=4π×轮廓净面积/总周长平方，不混入前景像素计数再强行截成1。3×3实心像素块的像素面积为9、轮廓面积为4、轮廓周长为8，圆度为π/4。
- 单点的轮廓面积/周长均为0；两点线段按闭合往返长度计，仍为零面积。这些轮廓的Circularity为null，不能用零替代；即使筛选范围包含0也不匹配，All/Any仍按各项有效匹配组合。
- BlobItem.Index为排序后连续一基位置；匹配候选仍使用零基下标，二者都不代表跨帧稳定目标ID。面积判定必须明确选中有效Index，不能默认首项。
- 无区域为NoMatch并带本次空快照；空快照可以通过数量0或预期不存在的判定。OK/NG均为本次成功计算，失败/超预算则不发布部分或旧结果。
- 无图判定消费独立区域数值快照，不重新借用原SDK资源；源模型/上下文释放后，已发布快照仍可读取与判定。

### 卡尺直线

- CaliperLineOperation的基准端点属于本次图像固定像素坐标；不是普通面积ROI或连续边缘带，不引用可编辑ILineSearchRegion。可用TemplateLocationBinding显式跟随定位；不隐式猜测全局定位或跨空间映射。
- 沿Start→End含端点均匀布置卡尺，单位切向为t，右法向n=(t.Y,-t.X)。Forward沿n从负半长扫到正半长；Reverse反转该扫描轴，灰度极性也按实际扫描方向解释。
- 切向按整数偏移平均2W+1个双线性采样值，法向采样间距1像素；内部浮点计算不归一化Mono16。高斯sigma为0或0.01..10像素，半径ceil(3sigma)，复制局部边界后只在未受截断影响的内部搜索差分峰。
- 对比度阈值是平滑/平均后相邻采样的原灰度差，不是梯度得分或0..1置信度。极性筛选后找绝对差局部峰，平顶并列取扫描较早位置；First/Last按扫描次序，Strongest按绝对差、再按扫描次序。
- 峰位置取相邻采样点中点，可能为半像素或插值坐标；这是离散采样估计，不宣称工业亚像素精度。
- 所有卡尺角点必须在图内，否则SDK前拒绝，不截断或以图外补边制造边缘。数量2..512、法向半长2..512、切向半宽0..32，总采样不超过500万；具体实现还可限制图像尺寸。
- OpenCV以Huber拟合初始化，按用户正交距离阈值选择内点，再L2拟合并逐轮剔除最大超限残差点。少于MinimumPoints为NoMatch；此方法不是全局RANSAC保证，不以残差小承诺真实精度。
- 结果包含零基卡尺Index、中心、选中边缘、扫描方向有符号对比度、UsedInFit及最终直线正交残差（像素）。无边缘或无最终直线时相应值为null，不能用0冒充有效测量。
- 直线端点为保留点在最终直线上的投影极值，方向与Start→End一致；RMS/最大残差只统计参与点。State明确区分有效拟合、边缘不足、内点不足及退化点集，NoMatch仍带本次诊断但Line为空。

### 卡尺边缘对测宽

- CaliperEdgePairOperation独立于找线拟合，按同一固定像素路径布置卡尺。采样、切向平均、法向高斯/差分及图内约束与卡尺采样一致，不借用另一操作请求或两条独立拟合线。
- LightStripe要求按扫描顺序先正灰度差再负差，DarkStripe相反；反向扫描后按新的次序配对。只考虑同一剖面内阈值后相邻合格峰，不跨卡尺，也不跳过其他合格峰拼宽度。紧邻异号峰不是同一个平顶，因此1像素条带仍能组成两边。
- 宽度=第二峰中点沿扫描轴的偏移−第一峰偏移，单位像素；不是对拟合线的距离、条带最短法向宽度、亚像素精度或毫米尺寸。条带倾斜需调用方正确布置扫描轴，本阶段不自动补偿。
- MinimumWidthPixels/MaximumWidthPixels是包含上下限的正区间，先筛选再选对。First/Last按扫描次序；Strongest取两边绝对灰度差中的较小值，并列取较早一对，不让单边强度掩盖另一弱边。
- 每把卡尺最多显式选出一对，记录零基Index、中心、阈值后峰数、可选对数、两边坐标/有符号对比度及宽度；无完整对时两边和宽度均为空，不发布半边测量。
- 完整选中对少于MinimumPairs则NoMatch，Statistics为空但本次局部诊断保留。达到门限则统计全部完整选中对的均值、最小/最大宽度与总体标准差，不自动剔除宽度异常，也不在卡尺间追踪稳定条带身份。
- 均值以ScalarMeasurementOutput的CaliperPairMeanWidth/Pixels输出，可直接进入显式单位公差；原上下文释放后仍可读/判定。汇总不等价于每个位置均合格，逐点验收仍须读取Samples。

### 圆卡尺与圆拟合

- CaliperCircleOperation在本次图像固定圆/圆弧上放置径向卡尺，正角为屏幕顺时针；全360度使用N个不同角度，不重复首尾，部分弧包含两端。角度区间不是拟合后覆盖角。
- Outward由内向外，Inward由外向内；极性相对该扫描方向，First/Last/Strongest及原位深阈值/切向平均/高斯差分沿用卡尺语义。名义半径必须大于搜索半长，不跨圆心，所有采样矩形角点必须在图内。
- 选点后采用最多64组分散三点的确定性共识初始化，不保证全局最优或穷举RANSAC。归一化坐标SVD求代数初值，再最多15轮径向几何最小二乘；残差超限点逐轮剔除，最多5轮回纳精化后合格点，避免种子偏差直接变成最终假剔除。
- 退化/共线和病态点集拒绝拟合；内点不足或覆盖角不足为NoMatch与空Circle，不返回旧圆。CoverageDegrees=360度−内点绕拟合圆心的最大角间隙，有限完整环采样通常也小于360度；MinimumCoverageDegrees须明确，不是精度或置信度。
- CaliperEdgeSample在找圆时的ResidualPixels为到拟合圆周的绝对径向距离，RMS/最大值只统计参与点。圆心、半径及直径单位像素；直径以CaliperCircleDiameter/Pixels标量进入公差，源上下文关闭后仍可读取。
- 名义搜索圆不等于测量圆；不以名义半径、最小包围圆或低残差宣称真实工件圆度/亚像素/毫米精度。可显式接入TemplateLocationBinding；不猜跨空间映射。

### 定位跟随测量

- CaliperLineOperation/CaliperEdgePairOperation/CaliperCircleOperation可携带TemplateLocationBinding；模型须为模板定义，原定位候选零基下标必须显式给出，空结果不能绑定。
- 未绑定时几何参数为当前输入像素坐标；绑定时构造参数的路径端点/圆心/名义半径/圆弧起始角为模板局部基准。Reference字段保留原值，运行几何只经所选候选TemplateToImage变换一次，不累积修改配方。
- 正等比缩放仅作用名义几何：路径端点映射，圆心映射、半径乘Scale、起始角加定位角度，圆弧跨度和数量不变。采样半长/半宽、sigma、残差限、边缘对宽度范围始终使用当前图像像素，不自动舍入整数窗口或把缩放当标定；若需变化须由调用方明确重建参数。
- TemplateMatchCandidate只允许可表示的正等比变换，不提供镜像/剪切。变换后的请求仍须通过有限、非退化、全卡尺在图内及预算检查，不补边或静默裁切。
- 统一引擎与Blob共用同一发布账本：只接受本引擎实际发布的原定位对象、原模型引用及实际搜索输入快照。相同GUID的新图像实例或重造定位DTO不能证明来源等价；不同帧/空间在构造时拒绝。
- 执行前/发布前都检查来源上下文未关闭、源模型仍当前；跨上下文测量中源修订被替换，迟到结果失败无Value。请求自身Model仍为空，不借源原生模型、不会自动重新定位或回退厂商。
- 跟随执行需源模型有效；已经发布的测量标量/几何是独立托管快照，来源关闭后仍可做公共几何/公差。两个生命周期不要混淆，也不授权派生图/裁剪直接复用定位。

### 无图几何与测量公差

- PixelPoint/PixelLine是有限、只读的像素几何值，注明FrameId和CoordinateSpaceId。PixelLine由间距大于1e-9像素且可表示的两点定义一条有向无限直线，不是有限线段或边缘对。
- 可明确从本次Blob的一基Index取得质心，或从有效CaliperLineOutput取得拟合直线；NoMatch直线不能转换。也可显式建立带来源标签的参考几何值；标签不证明传感器或模型真实，不授权图像分支等价或原生资源复用。
- 两输入必须同帧同空间；即使同空间但FrameId不同也拒绝，没有自动裁剪/派生图/世界映射。完成结果只持有托管值，原SDK上下文释放后仍可执行几何和判定。
- PointDistance为欧氏距离，PointLineDistance为到无限直线的无符号正交距离，不是最近端点距离，也不能称为边缘对宽度。距离单位像素，不暗中换为毫米。
- LineAngle必须选择无向锐夹角[0,90]度，或第一条Start→End到第二条的屏幕顺时针有符号角(-180,180]度；有向反向180度固定表示为+180。
- LineIntersection求无限直线交点，允许在输入线段或视野之外。以单位方向叉积绝对值作为SinAngle稳定性，MinimumSinAngle为1e-12..1、默认1e-6；平行/重合或低于该阈值为NoMatch及空Point，不能用任意点冒充交点。
- ScalarMeasurementOutput同时注明几何含义及由此确定的Pixels/Degrees单位。MeasurementRangeOperation显式指定与来源相同的单位，按有限闭区间比较；有符号角支持负上下限，但区间不在±180度自动包绕。
- OK/NG均为Success。非有限/溢出等不可表示运算为Failed且没有值，不发布NaN/Inf或旧结果；纯几何同样遵守上下文独占与取消后的发布守卫。

### 均匀比例标定与毫米长度

- PixelScaleCalibrationOperation输入同帧同像素空间的两个显式参考点、已知正毫米长度；像素间距至少1且可表示。UniformPixelScaleCalibration记录CalibrationId、参考点/帧、像素间距、已知毫米长度及mm/px比例，不准备假SDK模型。
- 单一参考长度仅建立均匀比例假设，不提供世界原点、X/Y独立尺度、二维仿射、透视或镜头畸变校正。两点精确约束没有独立验证残差，不伪造“零残差即精度已验收”。物理平面、镜头/焦距/采集分辨率及适用性由调用方确认。
- 标定适用范围为明确CoordinateSpaceId。该空间后续FrameId可以不同，表示同一像素几何下的后续曝光；不同空间（如裁剪、独立文件解码）拒绝，不从尺寸/文件名或相同像素值推断等价。相同标签不是物理条件未变的证明。
- LengthToMillimetersOperation只接Pixels长度及适用均匀比例，不接角度、面积或已换算毫米值。MillimeterLengthOutput保留SourcePixels和Calibration，ValueMillimeters必须等于像素值×比例；原标量的Kind/Value/Unit不修改，不允许只换标签。
- 换算结果必须有限非负，正长度下溢成0也失败无Value；真实0像素长度可正常换算为0毫米。标定比例本身为0/非有限也不发布。
- MillimeterRangeOperation只接引擎支持的带标定依据的毫米长度（均匀比例或仿射点距/点线垂距），通过IMillimeterLengthMeasurement保留来源帧/像素空间、CalibrationId和值，拒绝任意第三方裸毫米实现。按有限闭区间输出OK/NG；NG仍Success，保留完整测量追踪。原像素/角度MeasurementRangeOperation不被扩展成任意单位转换器。
- 定位匹配Scale不自动改变mm/px标定；目标尺寸变化与相机像素几何变化不是一回事。标定值为独立托管快照，不依赖源原生上下文存活，不代表真实工件精度或校准证书。

### 二维仿射点对与毫米世界坐标

- AffineCalibrationOperation冻结3..4096个CalibrationPointPair，像素参考点同帧同空间、已知MillimeterPoint同世界空间。变换方向固定为像素→毫米世界平面；世界原点、轴方向和物理平面由参考数据明确，世界轴必须为正交、同毫米单位的欧氏坐标，不猜Y轴翻转或把非正交坐标当欧氏距离。
- 使用全部参考点等权归一化Householder QR最小二乘，不使用正规方程求逆、不自动丢弃高残差点。MaximumResidualMillimeters为明确正毫米限，MaximumConditionNumber有限且在[1,1e12]，默认1e6。
- DesignConditionNumber是平移居中、公共跨度归一化后设计矩阵[u,v,1]的2范数条件数；TransformConditionNumber是仿射2×2线性部分的奇异值比。病态判定不使用与毫米比例相关的绝对行列式阈值；允许负行列式的轴向反转。
- AffinePixelCalibration提供PixelToMillimeters与MillimetersToPixel两份明确方向的系数快照、参考点、逐点毫米欧氏残差、RMS/最大残差及CalibrationId。使用发布的显式逆映射，不调用通用AffineTransform2D中像素级绝对阈值的Inverse/IsInvertible来重判毫米单位下的映射。
- 重合/共线、近共线病态、奇异/病态世界映射、不可表示系数或超最大残差返回NoMatch，并在AffineCalibrationOutput.Rejection给出稳定代码，Calibration始终为空。已可计算的残差保留；没有计算就为空，不伪造0。对有效映射，数值/反矩阵及残差之间有一致性守卫。
- PixelToMillimeterPointOperation只接适用像素空间；同空间后续帧可以复用。MillimeterPointToPixelOperation明确世界空间与目标FrameId，不能猜“当前帧”。映射不做视野或参考点凸包裁切；外推、镜头/采集分辨率和物理平面适用性由调用方确认。
- CalibratedPointDistanceOperation先使用两个带来源/标定的世界点再求毫米欧氏距离；两个观测须同帧同像素空间、同一标定快照，不能混用重标定修订。非有限距离或不同像素点映射成假0长度均失败无Value。
- 非等比/剪切下不能给裸像素距离、卡尺均值或圆直径乘一个“平均比例”。点距提供明确点对的世界距离，同一剖面的两边缘点距离不是世界条带法向厚度。另有完整映射直线后的点线垂距；仿射圆可能变椭圆，圆/椭圆尺寸仍须后续明确契约。
- 三点可恰好拟合，参考点残差不是独立验证误差；RMS为0不构成精度、标定板或校准证书。二维仿射不等于透视/镜头畸变/手眼标定；源上下文关闭后快照仍可用，不持有原生资源。

### 仿射世界直线与毫米垂距

- PixelLineToMillimetersOperation只接适用像素空间的PixelLine及明确AffinePixelCalibration，分别完整映射定义点得到CalibratedMillimeterLineOutput。Start/End保留有向无限直线定义，不是限制垂足的有限线段。像素拟合线的映射不等于按毫米欧氏残差重新拟合，原像素拟合残差不自动换成世界残差。
- 世界方向必须有限、非零；不使用单位相关的固定毫米长度阈值。映射非有限、方向因舍入压塌或无法归一化均失败无Value，不发布旧线，也不把NoMatch卡尺输出造为PixelLine。
- CalibratedPointLineDistanceOperation要求原像素点/线同帧同空间、同一标定快照。在世界欧氏平面用映射后的完整方向重算法向，输出非负无限直线垂距，反转方向不变。不能直接变换像素法向，不能给像素垂距乘平均尺度，也不把它改成垂足超出定义点时的线段端点距。
- AffineMillimeterPointLineDistanceOutput保留完整点/线及CalibrationId，加入毫米公差明确支持的第三种不可变长度快照。合法共线0距离正常成功；非共线像素几何被数值压成0、距离上溢/非有限均失败无Value。
- 剪切下同一扫描轴两边缘世界点距与点到世界边界直线的垂距不同。理想平行条带示例可验证法向厚度，但任意单个点线距离本身不能证明边界平行、处处等厚或真实工件质量。

### RGB编码值分割与二值检测链

- RgbColorSegmentOperation只声明8位Rgb24/Bgr24，RgbColorRange始终按R/G/B语义给出各自0..255字节闭区间；三个通道同时满足才命中255，否则0。通道存储顺序由实现明确重排上下限，不隐式转灰、线性化gamma或套HSV/Lab取值范围。
- 固定SearchRegion按像素中心规则栅格化，快照不受原可编辑ROI变化影响。域外始终清零，哪怕原黑色或白色也满足RGB范围；空参与域拒绝。第一批不接跨灰度定位分支跟随，不因相同CoordinateSpaceId放松原定位输入对象认证。
- 输出复用BinaryImageOutput：FrameId为直接颜色输入，Image为不同FrameId、同尺寸/像素空间的独立Mono8二值快照，不暴露Mat。全黑仍Success，不是NoMatch；Blob须明确选255前景，空Blob才为NoMatch且有空快照，随后0数量判定可正常通过。
- Mono8/Mono16/Float32/Bgra32未接通，不猜灰度或浮点范围、不静默丢弃alpha；可由调用方先选择已支持的显式格式转换。OpenCV本次预算2000万图像像素，不是原生内存硬隔离；HALCON未声明颜色分割，不借OpenCV回退。
- 编码值范围分割不等于感知色差、颜色一致性/校色认证或真实材料缺陷检查；照明、相机白平衡/编码与配方阈值适用性仍须真实样本验收。

### 显式对齐双图差异候选

- AlignedImageDifferenceOperation明确Current（主Image）和Reference两张独立Core快照，两者必须同宽高、同格式及同CoordinateSpaceId。不同快照不能重用同一FrameId；同一个对象作当前/参考合法，差异为0。没有全局/上下文隐藏背景。
- 同空间和尺寸只表示调用方声明已对齐，不能证明内容配准、光学条件或同一物理对象；不自动warp、缩放、归一化、配准或亮度补偿。不同空间/尺寸/格式须先另行明确处理，不暗猜映射。
- ImageDifferenceDirection.Absolute为|当前-参考|，CurrentBrighter为max(当前-参考,0)，CurrentDarker为max(参考-当前,0)。Mono8/Mono16使用原位深编码差，0..65535不落入有符号16位或归一化灰度；颜色/浮点未接通。
- MinimumDifference必须>=1且<=MaximumDifference，按正闭区间生成255候选；0差和反向饱和0不会被误当缺陷。Mono8上限不得>255，Mono16不得>65535。固定ROI冻结、按像素中心栅格化，域外0，空域拒绝。
- ImageDifferenceOutput保留当前FrameId、ReferenceFrameId、方向、原位深阈值及独立Mono8 0/255图，新图身份不同于任一输入，原尺寸/空间不变。引擎发布前核验两来源、参数、尺寸/空间与完成状态，不能返回旧背景的结果或把全黑记NoMatch。
- 全黑仍Success，后续以255前景做Blob，空Blob为NoMatch快照，0数量/存在性判定正常成功；其他区域可显式筛选、排序后按一基Index做面积/计数OK/NG。差异候选不是自动工件合格结论，不宣称抗照明/振动/错位。
- 每张输入预算2000万像素，两张输入与原位深临时Mat在实际调用结束前保持所有权；取消等待不代表可提前释放。本批没有自适应背景、SSIM、颜色差、模板缺陷或多帧统计。

### 单目标标准QR与精确文本判定

- QrCodeReadOperation只声明Mono8单目标标准QR（不是MicroQR/一维码/多码集合），整数矩形搜索窗为[x,x+width)×[y,y+height)，起点图内、每边至少21像素、不可越界；缺省宽高取起点到图像右下边界的剩余区域。不接受任意形状掩膜来伪造QR静区，不隐式转灰。
- OpenCV只调用一次DetectAndDecode，当前像素预算2000万，单窗400万且每边<32767。多码场景可能仅有原生选中的一个结果，不保证完整场景计数或稳定选择；应用须明确搜索窗，本批不声明MultipleMatches。
- QrCodeReadOutput的NotDetected为无文本/无四角，DetectedUndecodable为四角但无文本，Decoded为非空文本与四角；DetectedCount与DecodedCount只为本次单目标的0/1状态，不是场景中全部码数。前两状态NoMatch但保留空/检测几何快照，原生异常和错误输出为Failed无Value，不伪装NoMatch。
- 四角按原生循环顺序保存，必须有限、凸且非退化；坐标是原图像素中心，裁窗后明确加x/y，不加半像素、不默认某个点永远是图像左上角。发布窗口界限只容许1e-3像素数值误差，不是精度承诺；不发布错帧、窗外或状态不一致结果。
- 文本是该原生绑定返回的UTF-8解码字符串，最多8192个.NET UTF-16代码单元，超限失败不截断。不声明原始字节、ECI/GS1/FNC1解析、空载荷、结构化追加、纠错级/质量等级或所有码版本/曲面/镜面场景验收。原生空字符串不能区分所有解码失败原因，不拿空返回推导缺陷根因。
- QrTextEqualsOperation通过唯一引擎公共路径，使用StringComparison.Ordinal大小写敏感全文相等，不trim、不补空白、不大小写转换、不做Unicode规范化。源未解码或不相等为NG Success，保留原QR快照和期望文本；不需要假图/模型或SDK仍存活。
- 每次读取器/Mat/窗视图由当前调用拥有，等真实原生返回后再释放或处理取消，不共享读取器、不靠取消等待提前Dispose；已发布文本/四角为独立托管快照。真实刻印/低对比工件、照明/畸变/姿态鲁棒性须另验。

### 多QR返回集与成功解码计数

- QrCodeReadManyOperation为独立MultipleMatches能力，沿用Mono8、整数矩形窗/原图坐标与像素预算；MaximumCandidates为1..32原生检测候选预算。先DetectMulti，再对每个四角组独立Decode，未解码几何不丢弃，不把文本数组与几何短截错配。
- 原生点数须为非零4倍数（检测为false则须空），候选超过预算直接Failed无Value，不能截断后返回看似完整计数。任一原生异常/取消使本次整体不发布部分结果，候选间检查取消，原生调用结束前不释放。
- QrCodeReadManyOutput.Candidates只包含同帧/空间的Decoded或DetectedUndecodable快照，成功/失败文本与对应四角不可错配；相同文本不同位置保留，完全相同四角点集重复则拒绝，不暗做文本去重或空间NMS。
- 按原图四角Ymin、Xmin、Ymax、Xmax排序，同键保留原生顺序；零基下标不是跨次身份，也不做跨行归并。DetectedCount是本次返回候选数，DecodedCount是其中成功解码数，不是物理场景总码数或无漏检证明。
- 至少一个候选解码成功则读取Success，可同时有未解码候选；全部未解码或无候选为NoMatch，但保留完整返回集，不能丢弃检测框来假装无候选。源帧/空间、预算、状态及窗口几何均在发布前核验。
- QrDecodedCountOperation为公共无图成功解码数量闭区间判定，未解码候选不充数、NG仍Success，保留整个快照。具体内容比对必须显式选择Candidates零基下标后继续QrTextEqualsOperation，不默认首个成功或按文本合并。
- DetectMulti本身可能漏检，实测较严重数据破坏时单目标与多目标返回集可不同。解码计数只对所返回集有意义，不能由目标计数符合推断物理工件所有码都已发现。

### 8位量化HSV分割

- HsvColorSegmentOperation只接明确Rgb24/Bgr24，不猜输入已经HSV，不接受灰度/浮点范围或自动丢alpha。转换为普通8位HSV：H=0..179（约2°一档），S/V=0..255编码值；不是连续角度、0..1浮点HSV或H=0..255 FULL编码。
- HsvColorRange的H区间为闭区间：MinimumHue<=MaximumHue直接筛选，反之明确取[MinimumHue,179]并[0,MaximumHue]；0..179为全色相，H上下限相等只取该量化档，不自动变成整圈。S/V必须有序并同时满足。
- S8=0定义为量化后无有效色相，原生H8填0不代表红色。默认排除；IncludeAchromatic=true只在S范围也包含0且V满足时允许无色相像素通过，此时H不参与。最低S>0仍排除，不将显式开关凌驾于S/V阈值。
- 颜色分割使用已有固定SearchRegion、域外0、空域拒绝，输出BinaryImageOutput新Mono8帧、同尺寸/像素空间。有效约束无匹配仍全黑Success，不把范围放宽以求命中；后续可明确255前景接Blob/计数面积。
- H8/S8/V8为转换后的量化编码，边界受8位舍入影响，不提供精密色度/照明不变性或跨厂商得分等价。RGB编码的gamma、白平衡、ICC、光源与相机响应不被自动校正；Lab/感知色差和真实材料质量仍未声明。
- OpenCV最多2000万输入像素，临时源/HSV/多色相分段/无色相/ROI Mat由调用拥有并在真实结束后释放；预算不是硬内存或原生中断保证。不接跨灰度定位分支跟随，不放松原定位图像对象认证。

### AI分类结果发布边界

- AiClassificationOutput构造快照不等于后端输出自动可信；引擎发布时还须核验本次AiClassificationOperation的MaximumResults、MinimumScore、类别ID唯一性及分数非升序。超量、低于阈值、重复ID或错误排序为Failed无Value，不在发布边界重新筛选/排序/截断来掩盖坏结果。
- 类别ID按StringComparer.Ordinal比较，不能解析成数组下标、数字或先trim/大小写归一化；例如017与17可为两个不同ID。相同分数可合法保留，不推断唯一最优、概率已校准、分数和等于1或未返回类别的情况。
- 最低分数包含边界；没有保留预测仍为NoMatch空快照，不能与坏结果失败混淆。单项有限0..1分数仍由AiClassPrediction守卫，帧/空间和NoMatch一致性继续由既有发布层核验。
- 本批只修发布安全，不新增模型、类别业务判定或预处理；现有HALCON CPU仍限定原尺寸Mono8、模型1通道/0..255，不猜缩放/裁剪/归一化。模型准备、边界fake和真实推理是不同证据，真实模型/工件仍待提供及授权后验收。

### 已完成分类快照的显式判定

- AiPredictionJudgmentOperation必须明确Source、PredictionIndex、ExpectedClassId、MinimumScore，无缺省业务规则。PredictionIndex为源快照零基下标，不是类别ID或跨帧目标身份；期望ID非空，最低分数有限且在[0,1]。
- 只比较指定位置：Ordinal类别相等且Score>=MinimumScore才OK，不trim、大小写归一化、数字化ID，不搜索其他位置或自动换首个匹配类别。上下游分数阈值相互独立，下游无法恢复上游已过滤/截断的预测。
- 源为空或显式位置不存在（包括大于保留数量）为NG Success，ActualPrediction=null；不拿别的预测补位，不假造推理NoMatch/异常。参数无效仍在请求构造阶段拒绝，不扩大规则。
- AiPredictionJudgmentOutput保留完整源快照、显式规则、实际预测或null及Passed，帧/像素空间来自源；结果与规则不符不得发布。公共Vision执行域可在未配置AI或模型/原推理上下文已关闭时使用，不准备假图/模型或再调用SDK。
- 这不是AI推理或“正常/缺陷”类别映射，不宣称唯一最优、校准概率、完整后验或源权重真实性。真实模型/输入配方/授权前置仍未满足，不能以该判定链或SDK fake替代验收。

## 5. 结果、取消与关闭

- Success/NoMatch均带本次强类型结果；NoMatch用于本次未找到，不是错误，也不携旧候选。
- Failed/Unsupported/Cancelled/TimedOut必须有诊断、不得有Value。
- 区间阈值全黑图与灰度统计的0标准差仍是成功，不借用NoMatch表达。
- 调用方CancellationToken取消可抛OperationCanceledException。Cancelled/TimedOut状态保留表达相应实现结果的能力；枚举存在不表示已有超时调度器。
- 取消等待/超时不证明Native已停止，不得因此提前释放资源或发布迟到数据；实际行为和原生就绪由实现负责。
- ReleaseContextAsync/DisposeAsync等待真实任务结束并幂等；取消关闭等待仍须保留所有权，允许再次等待/重试。
- 每次返回的ValueTask只能消费一次；需要共享等待时，只调用一次AsTask并复用所得Task。取消关闭等待后应重新调用释放方法，不能重复等待已消费的ValueTask。

## 6. 执行值与文件格式

上下文、模型引用和只读像素缓冲用于进程内执行，不直接充当工程文件格式。
保存配方时应记录可重建的参数与资源标识，不持久化原生会话或运行中的上下文。
配置读取、资产文件和工程编解码由外层负责，不能反向放入Core。
