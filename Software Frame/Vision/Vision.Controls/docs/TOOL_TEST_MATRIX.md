# 逐工具单测与组合执行台账

版本：V1.18；2026-10-25。本台账与IMPLEMENTATION_PLAN V1.43、TOOL_EXPANSION_PLAN V2.18共同生效，不替换原测试项目/执行器。

## 完成定义（强制）

每个新增工具、算法模式或工具行为修复，必须实际完成以下门禁后，才能标为对应范围“离线完成”；不能只新增用例、编译、Validate或工厂创建就销项。

1. **S 单工具**：运行真实算法，使用独立定义的像素/几何预期；覆盖正常、合法零值/全黑、无输入/非法格式或参数、失败不残留旧结果。
2. **C 组合**：运行原FlowEditor流程，显式连接真实上游输出，确认下游收到它而不是原图；校验结果值/坐标、原图不被改写、上游失效清下游、失败停止；保存、关闭旧会话、重新打开后再执行。
3. **U 编辑**：原节点属性与原完整编辑器共用原DTO，A/B及不同任务不误写；运行/关闭拒绝修改，取消选文件不标脏；沿用Compact/MaterialDesign和原主题，不新增主操作栏、属性或文件框架。
4. **L 生命周期**：真实工作完成后释放；不能用fake替代原生模型/SDK资源的结论。
5. 先相关红绿，再现有逻辑/WPF/共享属性/受影响宿主门禁；命令、环境、数量、日志及截图写入验证记录。退出码0或测试摘要成功仍须检查日志是否存在未处理异常、测试宿主崩溃或实际零测试。
6. **SDK阻塞**：明确写“待授权/未完成”，保留RequiresRuntime与Skip。fake只记契约；不得自动换后端、装Runtime或下载模型抵扣。其他不依赖授权的子项继续执行。
7. 每批通常2–3工具；文件选择反馈可先做局部修复。每批之后核查至少两项相关官方来源。合成图精度/STA/截图不抵扣实图精度、物理鼠标/OLE、跨屏/DPI或现场节拍。

A2/R1a标量、R1b1线段、R1b2圆、R1b3单点→配方参考点已真实组合；当前一次仅一种覆盖，混合数值/线段/圆/点由原Card拒绝。合法原点/零距离与缺失分开，帧/直接来源锚点一致，不推断跨Crop等价。多规则公差/布尔组合、稳定项、有向角/通用多来源、世界单位/坐标链及一般多槽仍待；R1d2仅允许显式双点快照，R1e1仅双支撑线求交快照、R1e2仅双线较小夹角0–90度/Degree；复用原下拉，不新增框架。

R1c1已新增纯Core闭区间公差，测量Pixel标量与坐标身份明确、布尔false和缺失分开；原兼容ConditionNode两分支/原文件重开已测。仅单数值公差；R1c2已接入原Switch工具箱/规则编辑/严格输入和旧图兼容，28算法＋1原流程控制项。不把物理添加、多规则公差、稳定项等销项。

R1c3仅聚合数量：Blob最终Items.Count显式Count（非负整数）→原公差Count单位→原Switch。零数量成功/失败无值不混淆；Count不是稳定项身份，不需要Pixel坐标锚点，但仍要求同图像身份。旧字段默认空、公差默认Pixel不变。18逻辑（含1 HALCON托管契约）＋7 WPF新增实例；Native仍未执行。

A3/R2a仅显示格式桥代码/契约：HalconDisplayFormatTests新增13离线实例，公共Copy五格式奇数宽填充/独立缓冲、BGRA畸形输入在SDK前拒绝，旧算法紧凑Mono8限制不变。Native五格式×紧凑/填充共10例保持RequiresRuntime/Skip，**未执行**；不改变下表HALCON工具S/C待授权状态，不以显示支持抵扣算法格式或原生生命周期。共享三显示页/视口Capture离线STA1项通过，不调用正向HALCON。

A3/R2b仅domain边界代码＋显示配套：统一精确区域比较替代文件源面积判断，原生句柄兼容保留；六个受限/空/带孔/等面积偏移域Native用例只编译，尚无运行红绿。ImageConversionFailureTests一项真实WPF覆盖错误绘制/保绑定/借用对象/重载/新图恢复，原三显示页STA1项覆盖清屏；不抵扣原生域或资源S/C/U/L。

A2/R1d1仅唯一候选点：最终Items恰1时SingleCenter→原点测量，零/多个不输出点；不是行号/最大目标/跨帧稳定ID。BlobPointTests 10实例含真实矩形/筛选/原点/独立快照及world/machine不伪Pixel，HALCON仅2托管合约；VisionBlobPointTests 5实例覆盖原选择/命令/结果/0与5/重开/失败与歧义/旧State空字段。Native仍待授权。

A2/R1d2限定双点：原PointParameterValue附加可选ReferencePoint，Geometry声明能力，默认配方/旧接口保留；原State模式＋原第二点Connection/端口，不另建绑定。PointPairTests 12逻辑，VisionPointPairTests 5 WPF：真实两Blob0/50、原模式/来源行与可见文字、重开/旧单点文件、断线/删源/失败/歧义/异帧/同像素异Crop拒绝；R1d2不覆盖一般多槽/双线/Native，双线增量见下R1e1。

A2/R1e1限定双线：原LineParameterValue附加ReferenceLine，原模式/参考线端口与连接，原Geometry.LineIntersection按无限支撑线求交，Intersection沿原IPointResult继续消费；手动交点不伪Pixel。LinePairTests 12逻辑（先7红5绿），VisionLinePairTests 5 WPF覆盖真实Calipers/EdgeBand双线→交点→距离、原属性/可见来源/按钮/结果表与叠加、重开/旧手动文件、平行/失效/断线/删源/锁/异帧/同像素异Crop。补非有限求交失败守卫，不以公共数学抵扣HALCON Native；625/499/416/66、宿主增量0警告0错误。

A2/R1e2限定夹角：原LineAngle枚举末尾追加，原双线快照、无方向较小夹角0–90度，平行/重合0合法；NumericUnit.Degree=5与旧0–4分开，原公差同帧同锚点接收，错单位不转换。LineAngleTests 18逻辑先10红8绿后全过（含非整数角/0/45/90、方向反转、尺度/有限守卫/快照/枚举兼容）；VisionLinePairTests新增5 WPF（原5求交用例仍保留）覆盖两真实找线模式/原下拉/夹角→Degree公差→Switch OK/NG、重开/0、失效/错Pixel单位/同像素异锚点拒绝。643/504/416/66通过，宿主增量0警告0错误；不是HALCON Native、有向角或一般多槽。

E1/T04a原Threshold兼容扩展：Range=0/旧缺Mode仍含端点区间；Otsu=1/OtsuInverse=2仅全图Mono8，实际自动阈值可为0、失败/未执行null。自动模式拒绝MinGray/MaxGray数值绑定，不忽略旧连接；原IBackgroundImageTool独立快照、原Mode下拉/显隐/完整结果与文件。OtsuThresholdTests 17先13红4绿后全过；VisionOtsuTests 5覆盖真实正常/反向12/2548掩膜→Blob只找255的Count→同掩膜公差→原Switch OK/NG、重开/全黑0、错误帧/模式/格式/范围绑定和旧缺Mode文件。最终660/509/416/66、宿主增量0警告0错误；不抵扣HALCON Native/R4跟随或实图精度。

R4-A1仅OpenCV FindLine：新增限定ILineSearchFixtureTool，复用原定位连接/基准与Card后台；两模式路径由刚体推导不写DTO，只读派生编辑框不当面积域。UseLineFixtureBinding默认false、原选择定位源启用，断线/删源保true拒绝，明确关闭且断线才无跟随；模式存原State、来源只在Connection。FindLineFixtureTests 9先9红后绿，是真两模式旋转/平移算法、逆基准/有限守卫/独立快照；VisionFindLineFollowTests 6包含真实模板平移→找线→测长/原UI/重开、90度编程姿态会话、异Crop/旧帧/缺基准/失败/旧文件/断线不回退。669/515/416/66及宿主增量0警告0错误，不能抵扣HALCON或旋转匹配、FindCircle/mm/实图/物理验收。

R4-A2仅原Hough：ISearchRegionTool筛候选圆心而非像素裁域，ISearchRegionFixtureTool同步期望点；原Card位姿/独立快照与原动态定位模式复用，UseCircleFixtureBinding默认false/选择来源启用，断线删源保true拒绝。同帧/同直接来源、原ROI/期望点不变；旧无ROI/无模式文件仍全局就近。FindCircleFixtureTests 9先9红后绿，VisionFindCircleFollowTests 6含真实模板正/负平移→排除原位干扰圆→实际2R/原UI/重开、90度会话与程序化入口、域空/缺域/失效/旧帧/异Crop/断线/运行锁/旧图。678/521/416/66通过、宿主增量0警告0错误；不是像素ROI裁剪/环形卡尺/HALCON或旋转匹配验收。

## 当前28项目录的最小证据

S/C列表示已存在并在本轮门禁执行的范围，**不是全模式/真实工业样本验收**。源码中有RequiresRuntime用例不等于本轮执行过。

| 工具 | S 单工具证据 | C 组合证据/缺口 |
|---|---|---|
| OpenCV/ImageFile | ImageFileToolTests真实PNG四格式、Mono16 TIFF、异常/资源/后台快照/重读（9项） | VisionFileAcquisitionTests：原生命令文件→转换→阈值→Blob，重开/无关任务图/全黑/路径失效；不依赖HALCON |
| OpenCV/Threshold | 原OpenCvToolTests/NumericParameterTests范围快照保留；新增OtsuThresholdTests 17含两级/常量/0阈值、正常反向、旧Range/未知模式/格式拒绝、范围绑定拒绝、独立快照/释放 | VisionToolCombinationTests；A2真实统计→Threshold→Blob，0/80/重开/失效/单位帧/分数/断线拒绝；保留找线/找圆组合；E1/T04a VisionOtsuTests 5：Otsu/反向→实际掩膜→Blob.Count→同掩膜公差→Switch OK/NG、旧会话关闭重开、全黑阈值0/失效/错帧/范围绑定拒绝 |
| OpenCV/Morphology | OpenCvToolTests | Crop→Threshold→Morphology→Blob |
| OpenCV/Blob | BlobToolTests、Blob特征/ROI系列；BlobCountTests真实计数与失效；BlobPointTests唯一中心/筛选/原点与非像素拒绝，不择优 | 原组合/P2–P6保留；VisionBlobCountTests原字段与单位/来源下拉→Tolerance→原Switch，0与3数量OK/NG、原按钮/.flow重开、失效/旧帧/断线/删源/原DTO隔离；R1d1唯一SingleCenter→原Geometry点距离0/5/可见来源文字/重开，0或2候选撤点与测量 |
| OpenCV/TemplateMatch | TemplateModelWorkflowTests | 匹配→Blob定位、同裁剪输入、候选及重开 |
| OpenCV/FindLine | FindLineCaliperTests、FindLineSearchBandTests、OpenCvToolTests；LineParameterTests显式像素字段/非像素拒绝 | A0 Threshold→EdgeBand；A2/R1b1卡尺及旧搜索带→Common/Geometry真实端点距离、重开/失效/异帧异裁剪拒绝；R1b3另有Start/End→参考点真实距离/配置隔离与重开；R1e1另有两种真实找线模式→双支撑线求交→点测量/重开，平行/失效/异帧与第二线异Crop拒绝；R4-A1两模式实际模板平移→找线→长度约100/原路径不改/重开，单工具与编程姿态90度旋转、异Crop/旧帧/缺基准/无匹配/动态断线及删源拒绝已验；不是旋转匹配/HALCON正向 |
| OpenCV/FindCircle | OpenCvToolTests；CircleParameterTests真实圆/全黑、显式像素Circle、非有限/超int参数红绿 | A0 Threshold→Hough；R1b2 Hough→公共直径、半径26/38独立预期、原按钮/字段、重开/失效/同像素异裁剪拒绝；R1b3另有Center→参考点距离/合法0/重开；R4-A2另有9逻辑/6 WPF覆盖候选圆心域/期望点同步跟随、真实模板两方向移动→直径/重开、域空及动态断线不回退；不是像素域/环形卡尺精度或旋转匹配 |
| OpenCV/Crop | CropToolTests | 裁剪→阈值→形态学/Blob、同裁剪匹配定位 |
| OpenCV/ImageConvert | BasicImageToolsTests | VisionBasicToolsE1Tests，BGR/Mono16显式转换→滤波/阈值/Blob |
| OpenCV/ImageFilter | BasicImageToolsTests | VisionBasicToolsE1Tests |
| OpenCV/GrayStatistics | BasicImageToolsTests | 真实图像→ROI统计；A2/R1a四标量显式输出→阈值快照/Blob，数值单位与同帧身份；几何/布尔等R1b待办 |
| OpenCV/IntensityTransform | SegmentationToolsTests | VisionBasicToolsE1Tests真实灰度变换链 |
| OpenCV/AdaptiveThreshold | SegmentationToolsTests | VisionBasicToolsE1Tests真实局部阈值链 |
| OpenCV/ColorSegment | SegmentationToolsTests | VisionBasicToolsE1Tests真实颜色→掩膜/Blob/统计链 |
| Common/Geometry | R1e2另有LineAngleTests 18及VisionLinePairTests新增5覆盖双线较小夹角/Degree公差与Switch、平行0/有效NG/重开/失效/非整数，原枚举与配方不改。R1e1新增LinePairTests 12与VisionLinePairTests 5，显式双支撑线求交/Intersection点继续测距、平行/非有限失败、旧手动未知单位；非一般多槽。R1d2另有PointPairTests 12与VisionPointPairTests 5覆盖显式双点0/50及失效/旧文件，不代表一般多槽；旧公共数学3-4-5/合法0/交点(5,5)/平行失败，无图及无关BGR不走SDK；线段快照/单位/帧/坐标/NaN/退化等11实例＋统一契约 | VisionLineBindingTests 7实例：卡尺/旧搜索带→距离160/约161、配方仍5、原按钮/完整结果/保存关旧会话重开、原画布断线撤输入、删源/失败/运行锁/异帧/同像素异裁剪拒绝；另有CircleParameterTests 27实例、VisionCircleBindingTests 9实例覆盖Circle→2R、原配方0/99隔离/旧文件/混合槽拒绝；另有PointParameterTests 16与VisionPointBindingTests 10，覆盖三个真实点字段→固定配方参考点、合法0/缺失/旧配置/混合拒绝/数值溢出；R1c1 Distance/Diameter只对实际绑定/单位身份明确的结果开放Pixel标量→公差；不是工件长度/环形卡尺或HALCON授权精度验收 |
| Common/Tolerance | R1e2明确支持Degree，不转换Pixel；夹角0/45闭区间端点/有效NG及同锚点守卫，真实两模式→公差→Switch重开。ToleranceTests 18实例：闭区间端点/0/NG、无输入无布尔、快照/释放、非法值/单位/帧/坐标/界限、显式Gray域与真实几何字段 | VisionToleranceTests 6实例：真实Hough→直径→公差→原ConditionNode OK/NG仅选中分支、重开、0/断线/未选布尔字段停止、旧帧/单位/运行锁/同像素异裁剪拒绝；R1c2另有原Switch工具箱＋规则窗真实两路/重开；R1c3另有18逻辑/7 WPF覆盖显式Count公差/原Switch/旧图；不是多规则公差/物理添加验收 |
| HALCON/Blob | 托管Count结果与原目录白名单通过；唯一点/坐标映射托管合约通过；既有RequiresRuntime补Count/SingleCenter断言但未执行 | 原生S+C待授权，不以OpenCV同名工具抵扣 |
| HALCON/ShapeMatch | 托管契约；训练/文件/匹配原生用例未执行 | 原生S+C、角度/尺度/真实模型待授权 |
| HALCON/FindLine | 参数/卡尺几何契约；原生未执行 | 原生S+C及主链跟随待授权/实现 |
| HALCON/Barcode | 托管契约；真实读码未执行 | 真实码图S+C待授权 |
| HALCON/DataCode | 托管契约；真实读码未执行 | 真实码图S+C待授权 |
| HALCON/NPointsCalibration | 参数/点对编辑契约；原生未执行 | 原生标定及下游单位/误差消费待R5 |
| HALCON/Fixture | 纯刚体数学/托管契约，不将数学本身说成需要License | 节点级正向组合复核待补；跨裁剪/非单位尺度仍拒绝 |
| HALCON/ImageFile | 路径/JSON、无文件失败/不转换无关任务图契约通过；公共快照/完整域检查代码接通，Native未执行 | 正向Mono8/16/RGB→转换/阈值/Blob、浮点旧契约与域保护五实例列RequiresRuntime，未执行；**不得以OpenCV文件源抵扣** |
| AI/Classification | fake Provider契约/身份/释放 | 真实模型S+C未执行 |
| AI/Detection | fake Provider契约/结果互选/释放 | 真实模型S+C未执行 |
| AI/Segmentation | fake Provider契约/掩膜/释放 | 真实模型S+C未执行 |
| AI/AnomalyDetection | fake Provider契约/释放 | 真实模型S+C未执行 |

原流程控制Switch不是新视觉算法，不计入28目录。VisionDispatchTests 6实例和SwitchRequiredInputTests 2实例覆盖原工具箱添加/投放入口、原规则窗提交/取消/无效保存/运行锁/A-B、严格断线/未命中、旧常量结构与实际公差两路/.flow重开；物理OLE及主题矩阵仍待。

## 本轮执行与取图边界

- R4-A2找圆选择域/跟随最新678逻辑、521WPF、416FlowEditor、66属性/层次定向，均Passed对应数量、Failed 0、Skipped 0；Vision宿主显式增量0警告/0错误。RequiresRuntime由现有过滤排除，不能说其“通过”。
- FilePathEditor只负责选择路径，不读取图像/加载模型；默认系统OpenFileDialog，委托仅供宿主及自动化替换选文件交互。按钮路由、取消/异常/运行锁/迟到/A-B/原DTO/保存重开已STA验证；系统对话框物理操作未验。
- A0历史路径按钮组合经原宿主解码→任务输入→Threshold/Blob；A1新增OpenCV文件工具在算法层自行解码→公共输出→显式ImageConvert/Threshold/Blob，不反向依赖宿主，也不冒充HALCON原生读图。
- A1已实现OpenCV独立文件取图与HALCON ImageFileResult公共像素桥。后者仅完整域支持类型可输出，其他原生类型保留旧读图成功/尺寸/借用句柄并诊断公共输出不可用；原生S+C仍待授权。当前28项不等于28项均完成Native验收；Common/Geometry复用旧数学，不是新HALCON算子。
- FlowEditor首次414断言通过却在进程退出抛Loaded弱事件跨线程异常；共享测试STA改为xUnit集合Fixture收尾，正常退出后完整414连续三轮无该异常。不是生产生命周期重写，不捕获压制异常。
- 实际命令、首轮失败、截图、官方来源与未验收项见[验证记录](../../Tests/EDITOR_REFINEMENT.md)顶部V1.43；V1.42及更早保留为历史。
