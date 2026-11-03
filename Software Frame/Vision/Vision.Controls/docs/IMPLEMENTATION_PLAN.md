# Vision 视觉软件实施计划

版本：V1.43（R4-A2 Hough圆心候选选择域与期望圆心刚体跟随、真实模板平移→找圆→直径/重开/失效拒绝已验；678逻辑/521WPF/416流程/66属性，宿主增量0警告0错误；HALCON/旋转匹配/环形卡尺/一般坐标仍待）

## 0. 新执行主线：先直接复用节点属性界面，再补双后端工具

本节根据用户最新要求取代旧E0–E4的后续执行顺序；P0–P6、E1已完成的代码/记录保留，原十项未完成项不自动销项。工具逐项矩阵见[TOOL_EXPANSION_PLAN V2.18](TOOL_EXPANSION_PLAN.md)，完整编辑器接入见[TOOL_EDITOR_INTEGRATION](TOOL_EDITOR_INTEGRATION.md)。**禁止另造节点概要界面：节点属性直接用FlowEditor.Controls现有界面、PropertyGridVM与属性生成机制。**

### 0.0 V1.43：R4-A2 Hough圆心候选选择域与刚体跟随（2026-10-25）

- **口径冻结**：原FindCircle声明ISearchRegionTool，ROI只筛选Hough候选圆心，再按本次ExpectedCenter就近选择；不是像素裁剪/填黑、完整圆周约束、减少投票计算量或环形卡尺。无域保原全局就近，域内无圆拒绝，不回退域外候选。参数/目录键与原算法平滑、半径/阈值不改。
- **域/附加几何同步**：原面积域由SearchRegionPreparation准备；新增限定ISearchRegionFixtureTool扩展该旧能力，原Card复用本次位姿值对，在独立参数/像素快照中按current∘reference⁻¹同步ExpectedCenter。不能仅移动ROI却保留基准期望点选错干扰圆；不新建执行器、项目、属性控件或文件格式。Card内部位姿对由线专用命名扩为搜索准备，不改公开Session接口。
- **原UI与安全模式**：原ROI/定位来源/基准/匹配索引/完整窗口与紫色只读派生域沿用。UseCircleFixtureBinding默认false，原选择定位源启用、模式保State/来源只保Connection；断线/删源仍true拒绝，关闭但有线也拒绝，显式关闭且断开才无跟随。有限/同帧/Scale=1守卫保留，圆定位也要求同直接来源锚点，异Crop即使像素hash相同仍拒绝。
- **追溯与兼容**：AppliedExpectedCenter及原表新增执行期望X/Y，摘要明确圆心选择域；原期望点/ROI/半径不写回，新找圆配置不保存运行Fixture或派生点。受控旧无模式/定位端口/无ROI文件仍全图Hough就近，不承诺全部历史文件：旧已保存ROI现在具有候选圆心域语义，最多一个选定ROI，不再称其无效标注。原程序化VisionToolFlowNode遇新能力也准备期望点并恢复借用SearchRegion，避免只动域不动点。
- **真实证据**：新9逻辑先9红后绿：域内/域空/未裁像素、两个候选间期望点平移/90度旋转、非有限/缺域拒绝、独立快照及释放。6 WPF含真实TemplateMatch分别平移(60,40)/(-50,40)→目标Hough圆而非原位干扰圆→原Geometry实际2R，原圆心(100,100)/半径99及ROI不改，保存关闭旧会话重开、缺域/无候选/旧帧/无匹配/异Crop/断线删源/运行锁/旧无ROI文件已验。编程90度会话及原程序化入口是真Hough，不冒称旋转匹配。
- **门禁与余项**：原找线6定向保留通过，完整678/521/416/66、宿主增量0警告0错误；仍28算法/29工具箱，未装Runtime/解除Skip。HALCON找线/找圆/ShapeMatch正向、环形卡尺精度/一般坐标/mm/缩放镜像、实图节拍与物理UI仍待，R4不是全模式终验。详见验证记录V1.43、扩充V2.18/台账V1.18及定位矩阵。

### 0.0 V1.42：R4-A1 OpenCV找线刚体跟随（2026-10-25）

- **为何补限定契约**：既有ISearchRegionTool只表示面积域，不能把卡尺阵列/线搜索带当面积ROI套用。新增唯一Core小契约ILineSearchFixtureTool（借用原线参数几何＋独立准备方法），IExecutableTool可选成员默认null、OpenCV适配器透传；不改IVisionToolSession、原定位结果/连接/文件机制，不新增工具/执行器/参数框架。HALCON未声明该能力，不凭参数形状启用。
- **原数学/算法**：LineSearchGeometry按current∘reference⁻¹推导新端点，单位尺度保持宽度/数量/方向极性语义。FindLine准备时JSON复制原简单DTO与独立图像快照，只覆盖候选两端；原Calipers/EdgeBand算法及参数不改。新AppliedSearchStart/End只为本次追溯，真实测得线保持图像Pixel，不回变换成基准坐标。
- **原会话/画布**：原Card准备一个位姿值对并使用既有后台/revision/关闭等待；原定位来源/匹配索引/设为基准及完整窗口复用。只读紫色派生框复用ExecutionSearchRoi，表示变换后的编辑外框，不伪称面积算法域或逐把执行卡尺；基准黄/青几何仍编辑原DTO，派生框不进RoiEditor.Rois或参数文件。
- **动态必须显式**：原NodeProperty增加默认false的UseLineFixtureBinding。通过原定位来源选择时启用，模式存State、来源只存Connection；画布断线/删源后模式仍true并拒绝执行，关闭仍有定位线也拒绝，显式关闭且断线才无跟随。修复边界复核发现的原始断线后可能复用旧位姿/基准成功，已先红后绿；不将缺失当零位姿。
- **同域/持久化**：沿原定位帧/有限值/Scale=1守卫；找线另要求与来源同直接坐标锚点，两个Crop即使像素/hash相同也拒绝，同一Crop才可设基准。新找线导出只保存基准/模式/连接和原路径，不保存运行Fixture/派生路径；受控旧缺模式/定位端口/SearchMode文件仍EdgeBand原路径执行。
- **实际范围**：新增9逻辑先9红后9绿，含两模式平移/90度旋转、非零基准角逆变换、有限守卫/快照/释放；6 WPF覆盖真实TemplateMatch平移(30,40)→路径(70,90)至(170,90)→原Geometry测长约100，原路径(40,50)至(140,50)/配方距离5保持，原按钮/只读框/保存关闭旧会话重开/运行锁/缺基准/无匹配/旧帧/异Crop/断线删源已验。另一个公开编程姿态90度会话实例是真找线，不声称OpenCV matcher已估旋转。
- **门禁与余项**：加入断线模式守卫后完整669/515/416/66重跑通过，宿主增量0警告0错误，28算法/29工具箱不变。未装Runtime/解除Skip；HALCON找线及旋转匹配正向、FindCircle搜索域/跟随、mm/缩放/镜像/一般坐标链、实图精度节拍/物理UI仍待。原兼容VisionToolFlowNode仍按旧面积能力拒绝定位，不把该程序化入口称已开放线跟随。证据见验证记录V1.42、扩充V2.17/台账V1.17及定位能力矩阵。

### 0.0 V1.41：E1/T04a原Threshold的Otsu兼容扩展（2026-10-25）

- **授权阻塞下的既定离线子项**：落实扩充计划T04中“原Threshold兼容扩展Otsu”，不将A3 Native改称完成。原ThresholdParameters追加Mode，Range=0默认/缺旧字段保持含端点区间，Otsu=1全图Mono8自动正常二值、OtsuInverse=2反向，不新增工具/执行器/属性控件或文件机制；28算法/29工具箱不变。
- **不混用绑定**：原MinGray/MaxGray及其数值连接保留，自动模式不消费区间连接，仍接线明确拒绝，不能悄悄忽略来源/退配方。切回Range后仍消费本次Gray8快照，不改原99/100配方。自动模式不验证未参与计算的配方界限；不自动清改界限或绑定。
- **算法/结果**：复用OpenCV4.11 Cv2.Threshold/Otsu，输出独立Mono8 0/255，正常src>t、反向src≤t。AppliedOtsuThreshold为可空运行字段，合法0与未执行/失败分开；原AppliedMinGray/MaxGray仅Range有区间执行语义。表/摘要显示模式/实际阈值/Gray8与前景数，不将配方99/100冒称自动阈值。
- **原UI/快照**：原Mode下拉/INotifyPropertyChanged控制Min/Max显隐，条件只读JsonIgnore；原完整窗口借同Session。沿原IBackgroundImageTool准备独立模式/参数/像素快照，包含填充行打包；原Run仍同步，原IAsyncRelayCommand应等待完成，旧测试补await而非改生产命令求绿。
- **真实组合/同帧**：任务20/180图自动阈值20，正常前景12、反向2548，原掩膜连接→Blob.Count=1→Count公差→原Switch OK/有效NG；Blob只找255，不能拿原灰度图抵扣。公差图像也显式接同一掩膜，改回原任务图按原同帧守卫拒绝。保存关闭旧会话重开，全黑正常0/反向2560且阈值0合法；失败不残留掩膜/Count/旧布尔。
- **证据/修正**：新17逻辑先13红4绿后全过；5 WPF首次含测试可见集合/公差错帧配置失败，修测试沿真实连接后通过，未放宽生产同帧检查。首轮完整各1失败：新只读可见性条件须完整元数据及精确守卫登记，旧异步卡片测试需await；保留失败日志。最终660/509/416/66全过，宿主增量0警告0错误，实图精度/物理UI/Native仍未验收，详见验证记录V1.41/扩充V2.16/台账V1.16。
- **仍待**：Otsu仅全图Mono8，不扩16位/彩色/ROI或HALCON阈值；不是消除光照影响、亚像素测量前处理精度或节拍证明。A3资源/授权、R4找线/找圆刚体跟随、多规则/稳定身份与一般坐标链仍未销项。

### 0.0 V1.40：A2/R1e2双线较小夹角与Degree公差（2026-10-25）

- **复用双线**：原GeometryOperation末尾追加LineAngle=3，旧Distance/LineIntersection/CircleDiameter=0/1/2不变；沿原ReferenceLine/模式/两连接和原PrepareLineRun，不新增槽/端口/会话接口/工具。绑定夹角必须两条本次Pixel线，同帧同目标直接锚点，断线/失败不回退配方；距离/求交和旧手动文件门禁保留。
- **口径先固定**：无方向无限支撑线的较小夹角0–90度，反转端点不变；平行/重合是合法0度，与求交失败不同。归一化方向分量后用atan2(abs叉积, abs点积)显式转度，避免中间乘积平方溢出/下溢；方向退化、差值不可表示或非有限失败。原绑定线长度守卫不放宽；不是有向±180/0–360角或旋转姿态。
- **单位与组合**：NumericUnit末尾追加Degree=5，旧0–4不变，原公差明确支持Degree，不能拿Pixel/Count/Gray代替或隐式弧度转换。Degree仍同帧并校验直接坐标锚点（不因夹角旋转不变而推断跨Crop等价）。Geometry仅成功实际绑定两线且身份明确时输出AngleDegrees；手动夹角可显示Degree但无绑定标量。
- **原编辑/结果**：原Operation/字段/单位下拉、原数值连接→Tolerance闭区间→原Switch，NG为成功false、缺失为无值。表/摘要显示Degree与较小角口径，原叠加画两条实际线，不伪交点/角度弧。原配方四端点不改，运行值不进文件，完整窗口借原Session。
- **真实证据**：Calipers/EdgeBand各双线约90度分别OK/有效NG，保存关闭旧会话重开、再把参考源改同一线得到0且[0,0]通过。错Pixel单位、缺线、真实边缘不足失败均无旧公差；同像素全图Crop但目标锚点不同仍拒绝。新18逻辑先10红8绿后全过，5 WPF新增，最终643/504/416/66全过，宿主增量0警告0错误；之后仅加强非整数角度测试，完整643重跑。
- **边界**：仍28算法/29工具箱，未增加Native调用或解除Skip。HALCON正向/资源、一般多槽/多规则/稳定项、有向角/mm/一般坐标链、物理UI与实图精度节拍仍待。官方对照/命令/截图见验证记录V1.40，扩充V2.15/台账V1.15。

### 0.0 V1.39：A2/R1e1显式双线求交与交点继续消费（2026-10-25）

- **最小边界**：LineParameterValue附加非位置ReferenceLine、LineParameterDefinition附加默认false的SupportsReferenceLine；保原构造/解构/PrepareLineRun和会话接口，仅原Geometry声明能力。Distance仍单线长度，绑定LineIntersection必须有两条本次线，不能混用配方第二线或忽略附加线；手动未绑定求交保留。
- **原链与模式**：原属性“动态参考线”默认false，原参考测量线端口/来源下拉走ConnectionService与PortValueTransfer。模式存原State，来源只存Connection；启用后断线/删源/失败不回退，关闭但仍接线也拒绝。两线分别Pixel/有限/非退化/同帧/同目标直接来源锚点；同像素异Crop仍拒绝。
- **求交口径**：按无限支撑直线求交，交点可在线段端点外；平行/重合/退化不提供交点，原绝对叉积1e-12拒绝阈值保留，不承诺病态近似平行的计量精度。补非有限中间量/交点失败守卫，修旧手动极大有限端点可能发布NaN/Infinity的行为。
- **继续组合**：GeometryResult沿原IPointResult追加显式Intersection，仅真实双线绑定且单位/身份明确时输出Pixel点；手动求交不伪Pixel。原字段选择/点连接可继续供下游Geometry.Distance；表/摘要显示单位，叠加两实际线段与交点，不造几何工具或控件。
- **真实证据**：两个真实OpenCV FindLine分别以Calipers和旧EdgeBand取水平/竖直线，经原运行按钮求交约(99.5,59.5)/(99,59)，交点→原点测距约50；原两条配方线不改。保存关闭旧会话重开，失效/平行/异帧/同像素异Crop/运行锁与旧无模式/端口手动文件已验；完整窗口借原会话。
- **门禁**：新增12逻辑（先7红5绿后12绿）＋5 WPF，最终625逻辑/499WPF/416流程/66属性通过，宿主增量0警告0错误。缩短新模式标签区分点/线、增强第二线异Crop测试后完整499重跑。原28算法/29工具箱不变，两官方来源/命令/截图见验证记录V1.39，扩充V2.14/台账V1.14。
- **仍待**：只有限定双支撑线求交，不是角度/线段相交分类、一般多来源/多槽、稳定身份、mm或一般坐标链。公共数学不抵扣HALCON找线/Native；A3授权、实图精度节拍、物理鼠标/OLE/主题DPI仍未销项。

### 0.0 V1.38：A2/R1d2原点快照内的显式双来源（2026-10-25）

- **最小扩展**：PointParameterValue增加非位置ReferencePoint（null保原配方），PointParameterDefinition增加默认false的SupportsReferencePoint。保原构造/解构、旧接口与单点准备签名；只Geometry.Distance显式支持第二参考点，不新增工具/工程/调度器、会话接口、绑定表或通用参数槽。
- **原连接和独立模式**：Geometry沿原节点元数据增加默认关闭的UseReferencePointBinding与参考点来源/参考测量点端口。模式保原State，来源只保原Connection；原ConnectionService规划/循环检查和PortValueTransfer负责两点。启用时恰好一参考连接且本次有效；原画布断线/删源仍保持模式true并撤输入/结果，不能回退配方。关闭但仍接参考线也明确拒绝，需显式断线才使用配方。
- **两点同域**：主点/参考点均逐一校验Pixel、有限值、同ImageIdentity和目标直接来源锚点。不同Crop即使像素/尺寸/身份hash一致也拒绝；同点或原点/距离0合法，有限端点溢出仍按原距离守卫失败。不是双线求交、mm/世界坐标、一般坐标链或任意多槽，数值/圆/线与点混合覆盖仍由原Card拒绝。
- **真实组合**：两个原Blob以现有筛选各出唯一中心(12.5,22.5)/(42.5,62.5)，原下拉/模式/运行按钮经原Geometry得50，参考源选同一点得0。配方FirstStart(1000,1000)/FirstEnd(-1000,-1000)均不改；原表/叠加用实际两端点，保存关旧会话重开保模式与两连接，不带运行值。
- **失败与生命周期**：参考点0/2候选、参数失败、异帧、断线/删源/运行锁、模式与接线不一致均已验证。两PointValue及参数为本次不可变快照，原工具/图像变更和释放不影响已准备执行；完整窗口仍借原Session，不Dispose。旧无新State/端口的单点.flow默认false仍用配方得5，不自动绑定。
- **证据与范围**：11逻辑先11/11红后绿，补距离溢出实例后新增12逻辑；5 WPF含真实双源/0/50/重开、同像素异Crop拒绝、失效与旧文件。完整613/494/416/66通过，最后仅测试排布/删源断言增强后定向5再过；宿主增量0警告0错误，28算法/29工具箱不变。两官方来源/截图/实际命令见验证记录V1.38，扩充V2.13/台账V1.13。
- **未销项**：这是限定双点，不是稳定目标ID/任意多来源、多规则、双线/mm/通用坐标或A3 Native完成。HALCON找点/资源和物理鼠标/OLE/主题DPI/实图精度节拍仍待；本批实际为OpenCV生产点＋无SDK公共几何。

### 0.0 V1.37：A2/R1d1唯一候选Blob中心消费（2026-10-25）

- **授权阻塞下继续离线子项**：A3/R2域/格式Native仍待授权，不继续将编译数量累加当SDK完成。本批推进A2/R1真实结果消费：两后端BlobResult沿现有IPointResult声明SingleCenter，只有本次Success/RoiImage有效且最终Items恰好1个、中心有限、Image坐标时才提供Pixel点。空/多候选/失败/释放无点，不当原点，不自动取第一/最大目标。
- **不是稳定ID**：SingleCenter是“最终筛选后唯一候选”策略/稳定字段名，不是保存某行Index、结果选择行或跨帧目标身份；即使唯一候选Index=99也可消费。ROI/面积/特征筛选先于唯一性判断，保留原后端算法差异和HALCON中心原3位舍入。
- **坐标不伪造**：两BlobResult增加CoordinateSpace默认Image；既有ToWorld/ToMachine明确标Calibration/Machine，不改变映射数学。即使重新附加RoiImage，非Image结果也不能输出SingleCenter像素点。帧和直接来源锚点沿原点契约/节点执行快照验证，不开跨Crop/mm/世界坐标消费。
- **原链与配方**：两原目录只追加Point字段，旧字段默认空、28算法/29工具箱不变；原下拉/原点连接/原Card/原Geometry.Distance、本次FirstStart覆盖，FirstEnd仍固定配方参考点。真实矩形中心(12.5,22.5)到同点得0、到(15.5,26.5)得5；原配方起点(1000,1000)不改，保存关旧会话重开不携运行值。
- **失效与所有权**：0/2个目标仍是有效Blob分析，但不提供点，测量失败停止，不回退原点/配方/旧中心；真实参数失败/断线/运行锁也已验。已准备点/参数为独立快照，列表清空/原图像改写/工具和结果释放不影响本次闭包；完整工具窗口借用原Session，关窗不Dispose。
- **证据**：10逻辑实例先10/10红后绿（含2 HALCON托管/坐标映射合约，不抵扣Native），5 WPF实例覆盖原字段/参数/来源下拉及可见来源文字、原运行按钮/结果/0/5、重开、空/歧义/断线/失败/运行锁与受控旧State/端口恢复不自动选点。完整601/489/416/66通过，最后仅增强选中文本断言后定向5再过；宿主增量0警告0错误。
- **仍待**：HALCON Native唯一点断言已补原RequiresRuntime用例但未执行；A3、物理UI/主题DPI/实图节拍、多规则/多来源/真正稳定项身份/一般坐标仍未完成。原点几何混合槽与同帧同锚点门禁保持。详细两官方对照/截图/实际命令见验证记录V1.37，台账V1.12/扩充V2.12。

### 0.0 V1.36：A3/R2b公共图像域边界与显示拒绝诊断（2026-10-25）

- **统一边界代码**：ToVisionImage的灰度/RGB路径在Marshal.Copy前用GetDomain＋完整矩形＋TestEqualRegion精确比较；不只比面积，拒绝受限/空/带孔及等面积但偏移到图外的域。借用原图不释放，domain/full/equal临时对象using释放，不将域外存储像素冒称有效或清零。未做裁剪、填黑或隐式FullDomain。
- **文件源兼容**：移除ImageFile中仅按面积的重复检查，沿统一出口；原NotSupportedException回退不变——原生读取Success/尺寸/LoadedImage保留、公共OutputImage=null并带诊断，下游原机制拒绝空输出。公开转换签名、目录/状态/配置/文件标识不改；这是域安全行为改变，不宣称所有旧调用方都兼容非完整域。
- **显示侧必要配套**：三个既有显示页捕获预期NotSupportedException，复用原VisionImageViewport错误绘制。增加一个ReportImageError入口（非新属性/框架），清原输入/结果但不Dispose借用对象，SetCurrentValue保留绑定，重载保留错误、清屏/新图恢复；非预期异常不吞。避免新拒绝直接冒到WPF调度器，不用假黑图或旧结果填缺失。
- **离线真实证据**：新WPF用例先缺接口红，再取得清屏破坏原绑定的实际红，修后保绑定/借用对象/新图恢复通过；实际渲染错误与默认占位像素不同，并查看domain-acceptance/conversion-error.png。原三显示页STA1项加清屏诊断断言后通过；不是实际HALCON拒绝注入或Native色彩验收。
- **Native边界**：新增6个RequiresRuntime/Skip实例：Mono8/Mono16/RGB受限域、孔洞、空域、等面积偏移域，拒绝后原借用图仍可读；仅编译，未执行红绿。既有10个格式往返/文件域/浮点兼容原生用例也未执行；不能以591/484或截图抵扣HALCON域/资源S/C/U/L，A3仍待授权。
- **门禁**：591逻辑/484WPF/416流程/66属性＋1共享显示全过，宿主增量0警告0错误；最终绑定保护后完整484及显示1重跑。原28算法/29工具箱不变，原单值/Count/分支链回归保留；Native/物理UI/实图/节拍及稳定项/多来源不销项。详见验证记录V1.36、扩充V2.11/台账V1.11。

### 0.0 V1.35：A3/R2a HALCON显示格式桥（离线契约与代码接通，2026-10-25）

- **仅显示边界**：既有HalconImageConverter.FromDisplaySnapshot接入VisionImageSnapshot.Copy，Mono8/Mono16/RGB/BGR/BGRA先经公共尺寸/stride/缓冲/checked校验并逐行紧凑复制，再pin本次私有缓冲。正stride行填充不被当像素，负stride/浮点/未知格式仍拒绝；不添加新API/类型/包、通用格式框架或UI。
- **Alpha口径**：BGRA映射HALCON2311原生bgrx，明确丢第4通道，不做Alpha背景合成或预乘还原，返回原生三通道图；回读公共结果为RGB，不声称保留Alpha。Mono16仍uint2，不自动降位深/归一化。
- **旧算法不放开**：FromVisionImage仍要求紧凑Mono8；显式保留原stride拒绝，原HalconInternalTests也通过。显示新增格式不等于HALCON算法支持Mono16/彩色，更不扩Float32或一般domain语义。
- **资源**：只pin独立准备快照，原输入不改写；GenImage1/GenImageInterleaved沿原try/finally解pin，返回HObject仍归调用方。根据官方复制语义接通代码，但SDK分配/释放/Alpha与像素往返正向仍未运行，不能以托管Copy或pin代码替代Native生命周期验收。紧凑输入也多一次托管复制，其性能未测。
- **证据**：13新增离线契约先10绿3红，支持BGRA后畸形缓冲/短stride/溢出在SDK调用前正确拒绝；公共Copy五格式奇数宽/行填充及独立缓冲、旧算法限制均过。原Native往返4实例扩为5格式×紧凑/填充共10实例，Skip/RequiresRuntime保持，未执行。
- **门禁**：591逻辑/483WPF/416流程/66属性全过；共享DisplayImageMigrationTests 1项原三显示页/视口截图离线STA通过，未调用正向HALCON。旧display-retirement产物还原图缺当前FlowEditor引用，先构建失败未运行测试；仅从本机NuGet缓存重新还原生成依赖图后通过，未改工程引用或装Runtime。宿主增量0警告0错误，失败日志保留。
- **仍未完成**：A3/R2不是本批销项，HALCON正向S/C/U/L、实图/节拍、domain/浮点/其他资源桥仍待；A2稳定项/多来源/多规则、物理UI/主题DPI也不销项。原28算法/29工具箱不变。详细命令、两官方对照与资源表见验证记录V1.35，扩充V2.10/台账V1.10。

### 0.0 V1.34：A2/R1c3目标总数最小闭环（2026-10-25）

- **明确聚合而非项身份**：OpenCV/HALCON既有BlobResult追加只读Count与INumericResult，读取最终Items.Count；搜索域、面积及特征筛选之后、排除背景，不按原始连通域标签数或UI行号计数。Count不承诺跨帧目标ID、候选列表或排序稳定性，保留原连通/筛选/FillUp差异。
- **独立单位与合法零**：NumericUnit末尾追加Count=4，Unknown/Gray8/Gray16/Pixel的0/1/2/3保持。Count必须有限、非负、整数，不静默截断，不冒充灰度或长度；仍需同ImageIdentity，Count不是空间坐标，不加Pixel坐标锚点。公差显式选择Count，闭区间上下限可用有限小数，例如[2.5,3.5]接受3；输入数量本身不能为小数。
- **原链复用**：两目录只追加显式Count字段，原输出字段/原单位下拉/数值来源连接/单次快照/布尔输出/原Switch执行/.flow，不加目录工具或新框架。默认字段仍空、默认公差Pixel不变；旧无数值State/端口的Blob恢复原MinArea=10和未选字段，不自动选Count或绑定。
- **执行状态与资源**：只有Success且本次Blob已形成RoiImage时输出数值，默认空结果/失败/缺图/Dispose不输出0；成功空域或无前景输出有效0。不修改原Blob默认Success契约或新增运行标记。Count是不可变标量快照，来源列表清空/图像改写/原工具和结果释放后，已准备公差仍按原数量/界限执行，不借列表或像素。
- **真实组合**：0在[0,0]为OK；3在[0,3]为OK；3在[0,2]为Success=true/NG，只走原匹配支路。原下拉从Pixel改为Count并回写原DTO，原按钮/完整结果摘要显示单位，保存关闭旧会话重开再执行。未选字段/断线/旧帧/删源/真实参数失败不沿用0或上轮布尔；编辑运行锁不变。
- **门禁与范围**：首轮16逻辑2绿14红后补能力转绿，新增搜索域实例后18逻辑；WPF目录白名单/窄表单位摘要先红后绿，最终新增7 WPF实例。完整578/483/416/66通过，宿主增量0警告0错误；HALCON只有托管结果契约与注册，原生Count断言已补到既有RequiresRuntime用例但未执行，不抵扣Native S/C。
- **证据与未验**：原28算法＋1Switch不变，台账V1.9/扩充V2.9/验证记录V1.34。实看零数量OK与3目标NG的原页/完整结果及Count摘要。物理鼠标/OLE/主题DPI、实图/节拍、稳定项/多来源、多规则、世界单位/一般坐标与其余A3/R2–A8仍待。

### 0.0 V1.33：A2/R1c2原条件分发进入视觉工具箱（2026-10-25）

- **原控制节点**：从SwitchNode原FlowToolAttribute读取显示名/图标/分类/说明，追加同一NodePaletteItem到原FlowChartContainer工具箱。28个视觉算法条目不变，原“流程控制/条件分发”1项使工具箱共29项；不恢复隐藏ConditionNode、不把Switch包装成视觉工具/会话、不新建规则/属性/分支引擎。
- **添加兼容**：新AddNodePaletteAt仅为原NodeModel工厂/Chart.LoadNodeModel薄入口，原工具箱命令和拖放路由共用。旧AddPaletteAt仍返回VisionToolNode且仅处理视觉条目，未改返回类型或暗中添加通用节点。新Switch独立实例、坐标/选中/原属性/画布扩展按原机制，运行/关闭拒绝添加。
- **输入语义**：SwitchNode新增通用RequireInputConnection选项，默认false保旧图常量；仅视觉工具箱新建时设true。严格模式断线即报错，不能用上轮InputValue或面板常量。规则仍原1/2→OUT1/OUT2默认，公差布尔必须在原编辑窗口显式配置true→OK/false→NG；不偷偷重写匹配算法或默认规则。
- **原规则编辑**：仍EditDispatchRules→原WpfNodeEditorLauncher→DispatchRuleEditorWindow.ShowDialog；保存验证、规则对应端口同步、废弃已接线端口保留规则不改。取消/重复匹配无写回、A窗口选B仍写A，规则窗口打开/保存及ApplyRuleConfiguration运行锁已验证。运行中从UI打开提示返回，不抛入Dispatcher；直接构造被拒时Close撤注册，避免孤儿Window导致宿主退出超时。
- **实际链**：沿用真实Hough→直径→公差，工具箱命令添加原Switch、原模态窗口真实保存两规则、原运行按钮只走OK或NG；原.flow关旧会话重开保Require=true/规则/端口。断线但保旧InputValue仍拒绝，未命中和布尔未选字段均不运行下游。旧无新选项的受控.flow默认false仍可常量分发。
- **证据**：新增6项WPF与2项原FlowEditor测试；完整560逻辑/476WPF/416FlowEditor/66属性0失败0跳过、宿主增量0警告0错误。保留初次窗口清理失败/STA退出超时日志，不将两断言全过算绿；最终全量与定向退出均干净。最新红绿/截图/两官方来源/命令见../../Tests/EDITOR_REFINEMENT.md顶部V1.33，台账V1.8。
- **未销项**：原命令/程序化投放与模态按钮测试不等于物理鼠标/OLE、系统焦点、主题/DPI/跨屏；多规则公差组合/稳定项/多来源、多槽/mm/一般坐标链、SDK/实图节拍仍待。下一步按原R1残项或A3/R2格式边界推进。

### 0.0 V1.32：A2/R1c1闭区间公差与布尔输出（2026-10-25）

- **最小新工具**：未找到可复用公差工具，新增Vision.Core/Tools/ToleranceTool（纯BCL）与Controls薄适配，原Card/Session/.flow执行不变；不挂HALCON/OpenCV、不新建Common.Tools工程或工具基类。Common/Tolerance闭区间Minimum≤Measurement≤Maximum，默认Pixel，也可显式同域Gray8/Gray16；缺失输入不能执行，NG为有效执行Success=true/false，失败/未执行无布尔值。当前OpenCV14/HALCON8/AI4/Common2＝28，24传统/公共合约，原27项标识保留。
- **测量标量**：GeometryResult明确Distance/Diameter，只有实际绑定且具Pixel/帧/坐标身份的几何运算可输出；旧手动未声明单位的距离不伪标Pixel，运算不符/缺失不返回默认0。NumericUnit追加Pixel（旧值不变），NumericValue/ParameterValue附加非位置坐标字段，保留旧构造/解构形状。Pixel输入沿同帧+直接来源锚点校验，Gray8/16仍独立，不互换或自动归一化。
- **配置与结果**：Tolerance配方只存上下限/单位，无可保存Measurement或默认0。本次复制上下限与不可变测量快照，不写原DTO、不借用图像像素；原工具释放后已准备快照仍执行。原表显示OK、测量值、含端点上下限与单位，真实59.4像素在[100,200]为NG但不是执行失败；测量0在[0,0]为OK。无NaN/Infinity/乱序界限的假NG。
- **唯一连接**：原数值选择/ConnectionService/PortValueTransfer/Card继续工作；原节点增加明确布尔字段选择与bool?输出，原State保存字段，原端口存连接。布尔未选字段或无本次结果且已接线时，视觉生产节点明确失败，不能让原条件节点拾取上轮值/常量；不改变原ConditionNode的合法未绑定常量契约。
- **实际组合**：Hough Circle→Geometry.Diameter→Tolerance→原ConditionNode→OK/NG公共数学标记两链，分别只执行当前分支；原下拉/原运行按钮/原完整结果页，保存关闭旧会话重开可复现。ConditionNode是原隐藏兼容节点，本批按原节点模型加入/原连接并验证恢复，不恢复旧工具箱入口，也不宣称其他条件分发节点或物理OLE添加已经验收。
- **门禁**：18个公差逻辑实例、6个WPF实例含两分支、零值/断线、异帧/单位/运行锁与同像素异裁剪拒绝；新增目录条目进原逐工具属性/合约。完整560/470/414/66均0失败0跳过；宿主增量0警告0错误。最后仅结果OK列前置和测试留图顺序/坐标改进后完整470及6定向复核。两官方对照/截图/命令见../../Tests/EDITOR_REFINEMENT.md顶部V1.32，台账V1.7。
- **剩余**：只是单数值闭区间与单布尔字段；多规则/AND/OR、计数/文本/列表聚合、稳定项/多来源/mm/通用坐标链、默认条件分发UI、SDK/物理UI/实图节拍仍待。不因NG分支通过而完成整个R1或产品终验；下一步可按R1其余最小消费者/原分发入口或A3/R2授权边界继续。

### 0.0 V1.31：A2/R1b3单点到配方参考点（2026-10-25）

- **显式字段**：OpenCV/FindCircle提供Center，OpenCV/FindLine提供Start/End，均为当前算法已选结果的像素点；不泛反射任意子属性，不把显示行号当ID，也不将world/machine结果冒称Pixel。增加纯Core PointValue、可选IPointResult/IPointParameterTool；旧Session/Adapter默认空能力。仍27项目录，无新项目/工具/枚举值或调度器。
- **唯一原链**：点字段/点测量输入/点来源继续原NodeProperty、原ConnectionService/PortValueTransfer/Card、原State/.flow mapper/store；绑定FirstStart只覆盖本次起点，配方FirstEnd作为参考点保留，原FirstStart与FirstEnd均不被运行覆盖。参考点在原完整Geometry参数“第一组终点”编辑，增加说明，不另造参考点面板。
- **口径**：只支持Distance，单个来源点→配方固定像素参考点，不是两个动态点来源。参考点属于当前输入像素坐标，不随定位/世界坐标自动变换；必须同ImageIdentity和直接图像来源锚点。原点与相同点的距离0合法，缺失/失败/断线/删源/旧帧/异裁剪、非有限坐标/未知单位拒绝，不当原点或回退配方。
- **运行结果**：实际起点/参考点/距离/Pixel沿原表、原叠加显示连线和十字。三个真实链（Hough Center、卡尺Start、卡尺End）独立3:4:5布局预期约50，原配方起点(1000,1000)不变；保存、关闭旧Session、重开执行通过。真实检测点设为参考点后0成功，再断线立即撤输入/结果并拒绝运行。
- **数值修复**：两个有限极大坐标的旧距离计算可能返回Infinity仍Success；先有实际红测试，现Distance验证结果有限后才执行。不重写Point2D数学、不宣称任意极大数值稳健性；只是拒绝原算法不可表示结果。CircleDiameter/LineIntersection有效路径及旧enum/字段保持。
- **兼容与覆盖**：旧无Point State/端口的受控.flow恢复Distance=5，增加能力但不自动绑定原点；原Card同步/异步拒绝点与圆（以及其他多槽）混用。16逻辑实例与10WPF实例补齐真实S+C/0/缺失/单位/帧/坐标/生命周期、旧配置与混合拒绝；完整537/463/414/66均0失败0跳过，宿主显式增量0警告0错误。RequiresRuntime仍排除，真实SDK/实图/物理UI不抵扣。
- **剩余**：布尔/公差消费者、结果项稳定身份、两点/双线等多来源、多覆盖槽、通用几何坐标/mm链、R4定位跟随及授权SDK仍待；不以当前单点距离宣称全R1完成。下一批优先明确测量标量单位与公差/布尔消费，之后再扩稳定项/多来源。
- 最新命令/红绿/截图/两官方对照见../../Tests/EDITOR_REFINEMENT.md顶部V1.31，逐工具台账V1.6；以下V1.30及更早为历史。

### 0.0 V1.30：A2/R1b2圆最小闭环（2026-10-25）

- **范围**：OpenCV/FindCircle声明Circle字段，保存当前按期望圆心就近选择的真实Hough结果；不是候选列表索引或跨帧跟踪ID。复用Common/Geometry，在旧GeometryOperation末尾追加CircleDiameter（Distance=0/LineIntersection=1不改），本次仅圆心/半径快照→2R。没有新增工具/工程/调度器，仍27项；不以Hough代替环形卡尺、搜索域或定位跟随。
- **唯一原链**：增加纯Core CircleValue/可选ICircleResult/ICircleParameterTool，原Session/Adapter默认空能力。原NodeProperty圆字段/圆测量输入/圆来源下拉、ConnectionService、PortValueTransfer、Card与.flow mapper/store仍唯一主链。单次只允许一种参数覆盖，数值/线段/圆混合明确拒绝；不自动取消另一来源或择优运行。
- **实际值与配置**：GeometryParameters新增CircleCenter/Radius，CircleDiameter分支仅校验这些实际参与量；原点圆心合法，半径0/负数/非有限/直径溢出拒绝。绑定只复制本次参数，不写原配方；真实半径26/38合成图已有两条完整执行/保存关旧Session/重开证据，配方半径0/99分别不改。配方0在未绑定直径模式下非法，合法绑定仍可执行，证明不拿配方缺省阻断真实输入。结果表显示实际圆心/半径/直径/Pixel，原叠加画圆和直径。
- **安全边界**：复用现有ImageIdentity与TaskInput或直接NodeId+OutputPortId坐标锚点；旧帧与同像素异裁剪拒绝，同一明确裁剪输出可执行。缺失/失败/未选字段/断线/删源不当原点、半径0或配方直径198；原连接失效立即清BoundCircle和结果。手动公共数学仍不要求HALCON图像/License，未声明单位不伪标Pixel。
- **找圆输入修复**：原正负比较漏NaN/Infinity，且超int半径可能进入原生转换。六个有限性/范围用例在恢复本批修改前的校验条件时实际全红，补显式有限与int上限后通过；保留原floor/ceil半径、Gaussian+Hough算法、Canny/累加器阈值，不改精度断言求绿。
- **兼容与门禁**：旧无圆State、无新参数/端口的受控.flow恢复原Distance=5，新增能力端口但不自动绑定；旧Line/标量完整回归保留。新配置需要认识CircleDiameter的新版本，不声称旧软件可执行新语义。27个目录条目保留，521逻辑/453WPF/414流程/66属性0失败0跳过；宿主显式增量0警告0错误。RequiresRuntime仍过滤排除，未装Runtime/模型或接设备。
- **证据与剩余**：CircleParameterTests 27实例、VisionCircleBindingTests 9实例（含原Card同步/异步混合拒绝、旧配置），实际截图/命令/红绿及两官方来源见../../Tests/EDITOR_REFINEMENT.md顶部V1.30，台账V1.5。半径38样本Hough估计37.5，实际直径75，而不是伪造76；不是实图计量精度承诺。下一步R1b3点/结果项身份、布尔/公差与更多消费者，世界单位/一般坐标链/多槽与SDK/物理残项仍待。

### 0.0 V1.29：A2/R1b1线段最小闭环（2026-10-25）

- **首批仅线段**：OpenCV/FindLine声明唯一稳定字段Line，原节点输出字段/测量输入/线段来源下拉及原数据端口消费。既有GeometryTool距离分支沿Point2D.DistanceTo复用，新增Common/Geometry条目；既有HALCON命名空间/公开类型保留，只跳过无关图像转换，不新增Common.Tools工程、不宣称新增HALCON算子。当前OpenCV14/HALCON8/AI4/Common1＝27，原26项名称/键/默认Blob/模板回调不变。
- **唯一执行与配方**：原NodeModel/NodeProperty/ConnectionService/PortValueTransfer/Card/Workspace/mapper/store；仅增加可选ILineResult/ILineParameterTool与纯Core值契约。每节点一个字段/输入槽，绑定FirstLine仅本次快照把检测端点交旧距离运算，原配方(1000,1000)→(1003,1004)仍保存为距离5的配置，不被运行线段160/161覆盖。结果表明示实际端点、距离、Pixel单位，原结果图绘实际输入线段。
- **帧与坐标**：必须同InputImageIdentity，另捕获TaskInput或直接上游NodeId+OutputPortId作为保守坐标锚点；同一个裁剪输出可消费，两个不同裁剪原点即使像素/hash/帧/尺寸相同仍拒绝。不同处理节点的坐标等价不推断；目标显式图像来源必须与找线输入相同。不是通用坐标链或跨Crop映射，不开放mm/缩放/镜像。既有ToWorld/ToMachine现在标记结果CoordinateSpace，不能冒称像素Line。
- **失败与生命周期**：缺失/失败/未选字段/断线/删源不当原点或配方值；有限坐标/非退化线段/Pixel单位/同帧/同坐标锚点检查。求交的两来源绑定尚未开放，选择该操作后单槽绑定明确拒绝；旧手动点距离/求交API保留，手动零距离合法，单位未声明时不伪标Pixel。准备复制值和参数、不借用图像像素或原工具；原Card等待/撤迟到/释放链不变。原画布直接断线时曾保留临时输入，真实红测试捕获，现连接变更/上游失效即撤BoundNumeric/BoundLine及结果。
- **实测**：新增11个逻辑实例覆盖真实旧数学3-4-5/合法0/求交/平行，无图及无关BGR不要求HALCON图像；独立快照、已释放原工具、分支拒绝、单位/帧/锚点/NaN/Infinity/退化/未知字段。统一契约加入Common.Geometry（23传统/公共条目）。7个WPF实例包含两种找线模式真实链、原下拉/按钮/完整结果/保存关旧会话重开、配方不变、源失效/断线/删源/运行锁/异帧与同像素异裁剪拒绝；原每工具属性面板覆盖新增Common条目。
- **当前门禁**：494逻辑/444WPF/414原FlowEditor/66属性，0失败0跳过；宿主显式增量0警告0错误。RequiresRuntime仍过滤排除，纯数学通过不能抵扣HALCON找线/AI/现场设备。截图实际检查及红绿/命令/官方对照见../../Tests/EDITOR_REFINEMENT.md顶部V1.29，台账V1.4。
- **下一步**：R1b2点/圆/结果项身份、更多输入槽和消费者、布尔/公差仍未完成；先明确源几何/单位/坐标与合法零值，不用当前线长链销整个R1，也不抵扣R4定位跟随/真实SDK与物理UI。

### 0.0 V1.28：A2/R1a标量最小闭环（2026-10-25）

- **首批范围**：只开放GrayStatistics的Minimum/Maximum/Mean/Median四个稳定字段及Threshold.MinGray/MaxGray两个显式目标。每节点一个数值输出字段、一个消费参数；并非任意对象反射/多输入公式或整阶段R1完成。未新增目录工具，仍26项。
- **唯一原链**：NodeProperty仍在原NodeModel上；源码字段/目标名存原节点State，来源只存原Connection，原ConnectionService检查循环、原PortValueTransfer传递，原Card执行/等待/发布、原.flow保存重开。不新增绑定、属性、执行或文件引擎。
- **值语义**：新增小型Core契约NumericValue（有限数值、Gray8/Gray16单位、输入快照身份）和可选INumericResult/INumericParameterTool能力；只接受生产者声明字段。缺失/失败/无字段/断线/删除源不当0，启用绑定但无来源时不回退配方；只有明确选“使用配方参数”才恢复未绑定模式。Gray16不能当Gray8，NaN/Infinity、旧帧、非整数给整数参数都拒绝，不静默截断/归一化。
- **配置与运行分离**：Threshold仅在本次参数/帧快照中覆盖目标，不写保存DTO；普通未绑定Threshold同步主路径不变。原配方下限99，实际结果可明确为0或80，保存/关旧会话/重开后配方仍99。结果新增AppliedMinGray/AppliedMaxGray，在原结果表显示实际执行上下限，不用参数区的99冒充实际值。
- **输入身份限制**：沿用现有ImageIdentity（尺寸/stride/格式/帧号/时间/像素hash）做同输入快照核验；带填充输入的身份在压紧快照之前校验。它不是新几何坐标链，不能据此宣称跨Crop/世界单位或不同裁剪的几何身份已解决。ROI统计同图结果可显式用于该图阈值，不自动推断跨图映射。
- **兼容与生命周期**：旧会话/适配器默认空能力，无数值值时仍交旧Prepare/Run；新值送不支持的实现明确拒绝。未改旧接口签名、ToolIdentity/目录键、.flow/.visionproj格式；没有旧字段的节点State默认不启用数值连接，新配置的未知字段/参数严格拒绝恢复。新增接口默认成员只证明仓内现有实现可继续使用，不承诺仓外/ref struct或旧二进制可执行新语义。原revision/清理链撤销数值及图像下游，不强停任务。
- **界面收口**：继续整页原FlowEditor。截图发现动态来源改名后可能SelectedValue有ID却SelectedItem/可见文本为空；已用真实控件断言复现。共享原行增加只读SelectedOption投影及默认false的KeepOptionSelection元数据，仅新“数值来源”行显式启用，保留原Value绑定/样式，稳定选中项与显示文本；此行关闭按显示文本自动猜选，防同名跳到另一ID，仍可列表/箭头选择。未启用的其他宿主行通知/搜索默认不变，不全局改ComboBox行为。
- **实测**：10个逻辑实例覆盖真实统计/阈值、0、上下限、NaN/Infinity、单位/帧/整数、快照与原DTO/原工具释放；9个WPF实例驱动原下拉/原运行命令，真实统计→阈值→Blob（0及80）、保存关旧会话重开、非零、失效/缺失/断线/删源/旧帧、同名/改名与可见文字、运行锁。当前完整门禁478/436/414/66，均0失败/0跳过；宿主显式增量0警告0错误。末次仅收窄未启用行通知后9+66再通过。并行验证曾有1000节点编译计时106ms>100ms，保留失败日志，未改阈值；串行完整414连续两轮通过，不把性能计时称无波动。
- **剩余**：R1b布尔/点线圆/索引与几何单位/坐标身份、更多真实消费者和多数值槽尚未实施；A3/R2格式桥、计量跟随及既有SDK/物理UI残项不销项。新增标量契约不宣称真实HALCON/AI能力。继续按小批S+C门禁推进，而非注册空消费者。
- 最新代码/命令/红绿/截图、两来源对照与原始失败见../../Tests/EDITOR_REFINEMENT.md顶部V1.28；台账V1.2。下方V1.27/A1和V1.26/A0为历史，不以其中旧计数冒充本轮。

### 0.0 V1.27：A1实际交付与A2起点

- **OpenCV/ImageFile已注册**：新增独立文件源，复用原OpenCvToolBase、IBackgroundImageTool、ToolRegistration、ToolEntry、Workspace和IOutputImageResult。只追加目录键，不改旧25项；当前OpenCV14＋HALCON8＋AI4＝26项。默认Blob/模板回调不变，工厂/列表/关键属性测试明确保留并更新预期，不改成宽松数量断言。
- **真实取图范围**：BCL读取编码字节，OpenCV4.11 ImDecode(Unchanged)解码，支持Mono8、Mono16、8位Bgr24/Bgra32；逐行复制到独立紧凑托管像素，文件/Mat在单次执行结束释放。中文空格路径、PNG/TIFF、Alpha字节及16位不归一化有真实测试；浮点/16位彩色明确失败，无输出。每次运行重新读单文件首帧，不按EXIF旋转，不作多页遍历。
- 文件源不需要任务图；后台闭包冻结路径参数，不引用原工具或复制无关输入。原处理工具的null输入仍失败，未全局放开。结果由原Card拥有，下游借用同一输出；重跑/参数修改/关闭沿既有失效及等待链，不增加执行器。
- **HALCON ImageFile公共输出代码接通**：沿原ReadImage/GetImageSize/LoadedImage，再借原ToVisionImage复制完整域的Mono8/Mono16/字节RGB；不调用OpenCV挂名。不将无关任务彩色/浮点图先送旧Mono8转换桥。LoadedImage仍归工具，下次实际读取/释放前借用，公共像素独立。
- **兼容取舍必须保留**：旧HALCON读取成功的浮点/额外通道/图像元组等不能因为新增快照就被一概改成读取失败。只对不支持公共快照的情况返回原生成功与原尺寸/借用句柄，同时Message明确“公共输出不可用”、OutputImage为空；原端口准备明确拒绝空图，下游不回退。非完整矩形HALCON域不能偷偷丢弃或冒充域外已清零，公共输出同样拒绝。该分支有待授权用例，不冒称Native验证通过。
- **真实组合**：从原节点路径按钮写原DTO，经原容器运行命令执行ImageFile→ImageConvert→Threshold→Blob；Mono16/彩色独立像素与16像素区域/Bounds预期，初始任务输入为空。保存、关旧会话、重开后即使任务图是无关Float32仍用文件及显式连线；文件内容改成全黑可合法执行且Blob为空；路径改坏撤掉整链结果/借用输入并停止，不回退任务图。旧HALCON .flow不含新输出端口时可保留身份/路径并恢复现能力端口，未改mapper或文件格式。
- **门禁**：468逻辑、427WPF、414FlowEditor、65属性/层次定向，均Passed对应数量、Failed 0、Skipped 0；Vision宿主显式增量0警告0错误。五个新增HALCON RequiresRuntime实例未执行（被现有过滤排除），不是“通过”；原有Skip未删除。已查看文件源节点及完整编辑器结果图/路径按钮截图，展开动画后截图与原像素断言分开。
- **限制**：外部文件路径保存原参数，执行时相对路径按进程工作目录解析；文件不打包/不迁移/不监听，执行重新读取。编码缓冲＋托管快照有内存成本，未做大图节拍承诺。HALCON域/浮点/正向读图/跨后端原生链、AI模型及物理系统对话框/鼠标/DPI/跨屏仍待验。
- **下一步A2/R1**：已核查原NodeModel.SyncInputValues/SyncOutputValues仅处理节点直接属性，PortValueTransfer只传端口值，不解析Result子字段或单位/帧，也不禁止把旧值当默认值；不能靠一个“Result”端口宣称数值可消费。先解决显式数值字段→合法已有参数消费者及缺失≠0、单位/同帧验证、持久配置与运行覆盖隔离，再扩点线圆；不改通用层为视觉专用引擎，不造第二套绑定/文件机制。**本轮未实现数值消费**。
- 验证、源代码范围、官方两来源与失败记录见../../Tests/EDITOR_REFINEMENT.md顶部V1.27；逐工具现状见TOOL_TEST_MATRIX V1.1。A0/V1.26及下方R0–R7表作为历史与能力依赖保留，不反复重建R0。

### 0.0 V1.26：最新执行顺序与逐工具交付

本节优先于下方历史排期；无需再次等用户确认，在既有授权/兼容边界内执行。**R0原生整页复用不重做；不恢复视觉专属操作栏或第二套属性面板。** [逐工具台账](TOOL_TEST_MATRIX.md)区分真实算法、fake/契约、SDK待验，不以总测试数宣称全部工具就绪。

| 新批次 | 与旧计划合并后的实际工作 | 完成门禁/当前状态 |
|---|---|---|
| **A0 当前反馈与测试补漏** | ImageFile路径旁同行文件按钮；原节点属性与原完整编辑器共用原DTO；保留Compact/MaterialDesign/主题。补已注册工具S+C缺口 | **本轮已做局部代码**：取消/异常不改路径、不标脏；双向刷新、A/B隔离、运行锁、迟到拒绝、JSON/.flow恢复；真实文件→原宿主解码→Threshold→Blob；Threshold→EdgeBand找线/Hough找圆真实组合、旧会话关闭重开与失效。完整S/C/U/L规则见台账；物理UI不销项 |
| **A1 文件取图闭环，抽取R2子项前置** | 先OpenCV独立文件取图节点，声明实际支持格式/stride/通道/所有权；补HALCON ImageFile托管公共图像输出，保留LoadedImage借用API。接原IOutputImageResult/图像端口/目录/原文件链，不从控件或宿主反向引用算法层 | 下一代码批；真文件单工具→转换/阈值/Blob组合，彩色/Mono16显式转换、坏路径/损坏文件、原图不改、失效与保存重开；HALCON正向需要授权，不能调用OpenCV挂名。**本轮按钮不等于ImageFile原生S+C完成** |
| **A2=R1 最小结果消费** | 数值/布尔/点线圆显式选来源/项/字段/目标，单位/帧/坐标身份；统计→合法数值消费者、已有几何候选端到端 | 仍待实施；先核查真实消费者，缺失/旧结果不当0，不隐式截断或跨Crop推断；复用原PortValueTransfer/连接/保存/执行，不新造数据引擎 |
| **A3=R2其余格式与资源桥** | HALCON Mono8/Mono16/RGB/BGR/Alpha与公共图像/域/ROI/模型资源边界；补既有25项剩余模式复核 | 原生授权前仅记契约/UI；不全局放开未支持格式，不将显示支持算作算法支持；各工具独立S+C |
| **A4=R3双后端基础补齐** | 每批2–3项：转换/滤波/统计→阈值/形态学/裁剪→灰度变换/局部阈值/颜色分割 | 每项真实S+C/U/L和差异记录后才标对应范围完成；HALCON不能转调OpenCV；SDK阻塞不拖住无关离线工作 |
| **A5=R4计量与定位** | 找线卡尺/找圆搜索域及主链刚体跟随→边对/拟合/公共几何/公差 | 原P3/P7测量残项合并，不因本轮EdgeBand/Hough图像输入组合通过而销项；同帧/同坐标/单位明确，非单位尺度仍拒绝 |
| **A6=R5差分/映射/标定** | 双来源差分与刚体映射先于标定/去畸变、世界单位消费 | 坐标与图像共同输出、退化/域/误差明确；非线性映射不塞入Pose2D |
| **A7=R6识别条件阶段** | 既有码读取/AI闭环，按实际算子/许可/样本评估新增识别 | 无授权模型不加载/下载；四AI任务不冒充OCR；逐Provider真实S+C和释放 |
| **A8=R7十项终验收口** | 继承原十项矩阵，补物理鼠标/OLE、主题/DPI/跨屏、实图精度/节拍/真实SDK；资产去重、模型打包/相对路径迁移、任意帧局部编辑等残项逐一记录 | 不以STA/fake/编译抵扣；旧类型/URI/NodeTypeId/ToolIdentity/.flow/.flows/.visionproj v1/v2受控兼容，不擅删旧接口或改配置格式 |

**本轮代码事实与边界**：

- FileDialogFilter仅是原ToolProperty→原NodeProperty的显式字符串元数据；未改EditorType枚举值、DTO字段名或工具标识。两个编辑入口使用同一个小型共享FilePathEditor，不复制窗口/文件/属性框架，普通字符串不变。ImageFileParameters增加精确INotifyPropertyChanged，JSON仍只有FilePath。
- 选文件只写原路径，不自动读取图像/运行SDK/改任务输入。默认系统对话框以当前窗口为Owner；取消、错误、运行/只读或目标卸载/重绑后的迟到结果不写入。完整页ParametersPanel显式锁修改，保留其他区域浏览和原执行入口。
- 实际门禁：**453/422/414/65，均0失败/0跳过**；Vision宿主增量0警告0错误。FlowEditor首次退出出现弱事件跨STA异常，已在测试xUnit集合Fixture内正常收尾共享STA，修后414连续三轮无异常；不将VSTest退出码0而日志异常误记干净通过。依赖原有nullable/xUnit警告不被增量0警告掩盖。
- 已查看节点与完整页新截图，按钮/输入沿用原紧凑风格。系统文件对话框物理点击、暗色/DPI/跨屏以及HALCON真实ReadImage/公共输出组合仍待验；后续从A1继续，不回头重建R0。
- 两来源官方核查：.NET9 CommonDialog.ShowDialog(Window)的Owner/确认返回，以及WPF SetCurrentValue保持既有绑定；搜索、全文读取、直接HTTP200及快照见验证记录顶部V1.26。

### 0.0 V1.25：原生FlowEditor整页复用与R0代码收尾

用户明确要求操作栏不要新建，直接照FlowNodes.Workbench。已核实该宿主实际通过FlowEditorModule使用FlowEditor.Controls.FlowChartContainerView，不是引用另一个Workbench可执行文件。本轮按相同方式组合。

- VisionFlowView现只内嵌原FlowChartContainerView；操作栏FlowCommandBarView、工具箱、标签页、属性和输出区全部复用原界面，不复制XAML。旧VisionFlowView导航/程序化接口保留为薄适配，不新增Vision.FlowEditor或引用Workbench.exe。
- 原生运行/停止、循环、新建、单页与流程组保存/打开、导出/缩放等继续使用原容器命令。视觉仅提供25项目录投影、节点参数/来源元数据、任务图像委托和按节点Owned完整编辑器。旧视觉运行/文件命令兼容接口仍保留，主页面不再据此绘制第二套操作栏。
- 来源/定位/索引/基准已移到原节点属性；操作直接作用于该节点的原连接、FixtureIndex和ReferenceFixture。下拉保存NodeId，显示名仅呈现；空选项不回退原图，非法来源/执行锁仍拒绝。设基准命令绑定具体节点，不借全局SelectedNode误改B；不支持ROI定位的工具不显示定位配置。任务图像从选中节点属性的“加载任务图像”加载到该节点所属页，空页先添加节点；原零节点输入的程序化保存/恢复能力保留。
- 原属性元数据增加动态显隐，沿用原PropertyItem/Group/Host VM，不增加节点概要控件；找线在卡尺模式显示阈值/卡尺数，在旧搜索带显示Canny阈值。搜索隐藏行也能刷新；共享选项复用EnumOption值/显示名分离，仍支持旧string候选。DTO不复制，运行/只读与独立编辑器浏览边界不改。
- 原容器多页创建/切换/关闭及.flow/.flows恢复均纳入视觉所有权；输入按页隔离，运行清旧结果，关闭等待原实际调用。源码测试证明原按钮持有原命令。坏流程组准备所有候选成功后才替换旧页，失败释放候选；DocumentProperties保留空任务输入，原流程组命令不绕过运行/文档锁。
- 同任务视图重新放置不释放工具；换DataContext关闭旧借用窗口但旧任务仍有效。AutoWire产生的未使用空容器释放其日志路由。窄屏布局/分隔条/命令外观按原FlowEditor，不再维护视觉专属ToolboxPanel自动收起逻辑。
- 本轮门禁：451逻辑、417WPF、414FlowEditor、64条属性/层次定向，均0失败/0跳过；Vision宿主显式增量0警告0错误。额外FlowNodes.Workbench编译0错误、139警告，未启动设备宿主。已查看新原生页面STA截图；物理鼠标/OLE、跨屏/主题DPI及授权SDK仍不销项。R1–R7未实施。

### 0.0a V1.24首批交付记录（历史，顶部布局/来源剩余以上文为准）

- 已将主流程右侧常驻完整编辑器移除，直接放置与FlowChartContainerView相同的PropertyGridHost及PropertyGridHostViewModel，TargetObject保持SelectedNode.Model；未创建节点概要控件或第二套属性行。Vision.FlowNode的VisionNodePropertyGridBuilder仅委托原Builder并追加显式白名单中的原DTO属性行。
- 节点属性按钮、节点动作/右键及节点双击共用原INodeEditorLauncher/INodeEditorHandler。Workbench视图创建每节点一个Owned非模态窗口；只借用原Workspace.Editor/Card/DTO/ROI/结果，不调用工厂/加载模型。重复打开激活，A窗不随选B重绑，关窗不释放会话；删节点/任务关闭先收窗口，再沿原等待/释放链处理。
- 工具目录显式登记NodeParameterNames：大多数2–4项；GrayStatistics/码读取/ImageFile本来只有一个相关标量字段，不补假参数；NPointsCalibration点对表保留在完整编辑器。真实参数/ROI编辑标任务修改并清旧结果，选择/运行不误标脏；Blob专用适配可重建，但工具/Card/原DTO/ROI身份不变。关键参数保存到原.flow，关旧会话后重开已有回归。
- 原PropertyGridHost只增加默认true的IsEditingEnabled属性，锁属性/端口修改，保留搜索/滚动及完整编辑器按钮；其他宿主默认行为不变。主题入口保留在主任务工具栏。
- **R0尚未整阶段销项**：图像/定位来源、候选索引与基准目前仍在顶部明确标注的“选中节点配置”栏，未迁入原节点属性元数据；完整窗口不复制这些全局选中命令，避免A窗误写B。模式相关关键字段显隐、更多窗口重载/主题/DPI及真实鼠标/跨屏验收继续收尾，不以STA截图替代物理UI验收。下一步仍R0，不提前开始R1或HALCON扩充。
- 实际451逻辑/413WPF、414FlowEditor、62条PropertyGrid/层次定向测试通过，0失败/0跳过；宿主显式增量构建0警告/0错误。完整WPF后只加强原R0测试的保存/关闭/新会话重开断言，4条再通过，未改生产代码。日志、命令、首轮失败和取舍见../../Tests/EDITOR_REFINEMENT.md。

### 0.1 V1.23规划时已核实的起点（历史，不是本轮现状）

- 当前HALCON8、OpenCV13、AI4，共25个后端条目；工具箱在左侧。主流程右侧仍常驻完整VisionToolEditorView，尚未分离。
- FlowChartContainerView已装配现成属性面板，其PropertyGridVM.TargetObject随SelectedNode.Model变化。VisionNodeEditorHandler现在只Select节点，并没有打开独立编辑器。
- HALCON算法桥FromVisionImage仍只接受紧凑Mono8；显示桥支持更多格式不代表各工具可处理彩色/16位。ImageFileResult当前只含尺寸/路径，原生LoadedImage为借用对象，尚无公共输出图端口。
- 原结果出口、图像端口和定位端口已存在，但数值/几何字段选择、带单位消费和测量工具跟随尚未完成；不能凭GetProperties或Result出口宣称已贯通。

### 0.2 最终界面与所有权

```text
主任务：左侧现有工具箱 | 中间现有流程画布 | 右侧现有FlowEditor节点属性
                                                  └─ 打开完整工具编辑器
节点双击 / 右键编辑 / 上述按钮 ── 同一INodeEditorLauncher入口
                                      ↓
                         Workbench拥有的工具编辑窗口
                         内嵌原VisionToolEditorView
                         借用打开时该节点的同一Session/DTO/ROI/结果
```

- FlowEditor.Controls不引用Vision.Controls，不增加视觉品牌判断；Vision.Controls不负责流程调度/窗口全局缓存；宿主负责窗口创建、Owner及激活/关闭。
- 现成节点属性面板负责名称、执行/端口配置、输入/定位来源、少量关键参数和状态摘要。必要补充仅在Vision.FlowNode适配原节点属性机制，直接代理原DTO，不新建VisionNodePropertyView/概要PropertyGrid/第二份参数对象。
- 关键参数按显式元数据白名单决定，首期每工具约2–5项：如Blob灰度范围/最小面积、匹配分数/候选数、找线阈值/卡尺数、滤波模式/核尺寸；不存在的能力不补假字段。不按Name或Backend字符串猜能力，不把Workspace/Session全部反射成节点属性。
- 图像/定位来源、设基准等命令必须绑定明确节点；A窗口打开后选中B，不得让A的操作经全局SelectedNode误写B。真实参数/ROI/模型修改标记任务修改并触发原刷新/失效，仅运行/选择变化不误标配方修改。
- 完整Vision.Controls负责图像、全部工具参数、ROI手势、模型训练/加载、详细结果及“执行当前工具”；不再常驻替代主流程属性区。任务绑定模式隐藏单工具工程打开/另存，.flow仍由主任务保存；独立Controls宿主原工程能力不删。
- 默认一个节点一个非模态Owned编辑窗口，重复打开激活原窗口；绑定打开时的节点，选中B不把A窗口悄悄改绑。以任务会话＋节点对象/身份区分，不跨重开任务复用旧窗口。首期不引入Docking/窗口持久布局框架。

| 对象 | 创建/拥有者 | 借用者 | 关闭规则 |
|---|---|---|---|
| 工具/Card/参数/ROI/结果/EditorSession | 原节点Workspace/任务会话 | 节点属性、完整编辑器 | 仍由节点退役/任务关闭释放，等待实际调用完成 |
| 编辑窗口与视图 | Workbench宿主 | 用户 | 关窗仅结束视图订阅/绘制手势，不Dispose借用会话，不停止流程 |
| 节点属性面板及VM | 原FlowEditor装配 | 视觉节点 | 保持原选中节点/属性生成生命周期，不能把目标换成完整EditorSession |

### 0.3 阶段与门禁

下表保留V1.23能力依赖定义；当前执行顺序/状态以上方V1.26 A0–A8为准，文件取图子项已前置，不再次等待R0代码重做或用户确认。

| 阶段 | 实际范围 | 前置与完成条件 |
|---|---|---|
| **R0 界面分层，第一步** | 主流程移除常驻完整编辑器，直接使用现成FlowEditor节点属性；按钮/双击/右键统一打开原Controls编辑器；关键属性代理原DTO，编辑后标任务修改并清旧结果 | 25项仍可编辑/执行；反复开关不创建工具/加载模型；A/B不误写；概要与完整页同步；运行只锁修改；删节点关对应窗；宿主关闭先收窗口再按原关闭链等待/释放；.flow/.visionproj兼容。旧内嵌布局断言按新需求替换为现成属性面板/独立编辑器行为断言，原像素/生命周期断言不减 |
| **R1 结果连接最小闭环** | 显式选来源节点、结果项/字段、目标输入；数值/布尔/点/线/圆与单位/坐标身份；复用原PortValueTransfer、连接、文档和执行器 | 先用已存在的统计→阈值/配置数值、找线结果→现有几何候选做端到端；必要的旧几何接入列为单独适配，不先造空消费者；缺失/旧帧/无结果与有效0区分，禁止显示字符串/排序行号绑定或静默double→int截断；每项连接保存重开 |
| **R2 HALCON格式与会话边界** | 按工具声明补Mono8/16/RGB/BGR/BGRA输入策略、stride、公共图像输出、帧信息/域语义；补ImageFile公共像素出口；Runtime/License不可用清晰提示 | 不因显示转换可用就全局放开老工具；BGRA先明确Alpha语义；所有新HObject/HTuple/句柄有所有权；按算子线程能力决定后台快照，不照搬OpenCV并发；错误/重复加载/关闭/迟到释放验证；旧接口/已有格式拒绝契约受控迁移 |
| **R3 双后端基础工具补齐** | HALCON补现有OpenCV的转换、滤波、灰度变换、阈值、自适应阈值、形态学、裁剪、统计、颜色分割；OpenCV完善现有6项的适用范围；功能分组沿用现有工具箱 | 按矩阵分小批交付，每批2–3个；先转换/滤波/统计，后阈值/形态学/颜色。参数/单位/边界逐项定义，HALCON实现不调用OpenCV伪装；uint2原始统计不准用自动归一化直方图冒充；每批有真实SDK门禁或明确标待授权，不因编译/fake通过就称HALCON已验收 |
| **R4 双后端定位与计量** | 先OpenCV/HALCON已有找线的刚体跟随，再找圆；HALCON补找圆；独立边对卡尺、点集拟合；复用既有Geometry/TransformPoint候选，补公共几何/公差消费者 | 共享像素/单位契约但不复制原DTO；派生几何不覆盖基准；先同输入单位尺度刚体；Hough找圆不冒充环形亚像素卡尺；重复线/退化圆/无边/越界/离群点与本次性均有回归；纯数学/公差不各写OpenCV/HALCON两份 |
| **R5 双图、坐标与标定** | 双后端差分/刚体图像对齐；OpenCV补N点标定，双后端棋盘格标定/去畸变；参考图/标定资产保存 | R1及所需坐标/资产契约先通过；逐节点明确输出→输入映射与有效域；去畸变非线性映射不塞入Pose2D；有有效标定链才输出mm；参考资产不混装TemplateImage；旧配置迁移/原子失败保旧；不先放开跨Crop/缩放/镜像现有拒绝 |
| **R6 识别与检测补强（条件阶段）** | HALCON现有条码/二维码完善；审查AutoBarcode/CannyLine/LineGroup是否接入或并入模式；OpenCV先评估随包QR能力；OCR/印字按引擎/模型/样本单列 | 不宣称OpenCV原生自带完整OCR/所有码制；新包/模型/许可需先确认。HALCON OCR能力受实际2311 Runtime/授权约束；AI四Provider任务不是OCR；印字质量与文本一致性分别验收，保持现有AI/第三方扩展 |
| **R7 产品级收口** | 双后端及混合链、现成节点属性＋完整编辑器、错误/资源/配置/主题/DPI、实际授权SDK与代表实图、节拍报告 | 25个旧条目不丢失，兼容标识/文件不破坏；日志可追溯后端/帧/单位/失败来源；未授权项不能总验收打勾；逐条回填原十项矩阵与本计划矩阵 |

**依赖顺序**：R0先完成；R1、R2可按最小切片并行；R3依赖所需R2，R4依赖所需R1＋输入/ROI边界；R5依赖坐标/资产设计；R6在实际环境确认后实施，R7贯穿各阶段。遇到SDK授权阻塞，可继续不依赖它的OpenCV/界面工作，但HALCON阶段保持阻塞状态，不自动回退OpenCV。

### 0.4 工具和文件的兼容约束

- `.flow`、已有NodeTypeId/ToolIdentity、旧目录键和有效`.visionproj` v1/v2保持。没有任何工具自动换后端；后端替换是显式操作，有模式/单位/模型兼容检查，不适用项拒绝并解释。
- 主任务保存统一捕获原DTO；窗口不持有另一份可保存配置。模型加载仍显式，不自动执行或反序列化原生句柄。
- 新结果连接只保存引用与配置，不保存运行结果、SDK对象或DataTable；确需新增配置版本前先写旧样本迁移与失败保旧方案。
- 不增加Vision.FlowEditor/Vision.Runtime/Common.Tools工程，不引入第二套属性/执行/文件/插件框架；纯几何数学复用Core既有能力，旧HALCON包装保留兼容。

### 0.5 交付、授权与排期

- 每个阶段实施前冻结涉及的工具、首期模式、格式、单位、ROI、可用性、文件字段和非目标；每批闭环后再扩张，不以图标/条目数量为KPI。
- 默认普通环境只执行离线合成/授权样本、fake与无Runtime契约；HALCON真实算子、模型/许可证、相机/生产环境及安装依赖未经具体授权不运行。工具箱可展示能力与不可用原因，不能自动加载模型/探测硬件或伪造成功。
- 门禁：按影响执行逻辑、WPF/STA、属性/窗口生命周期、旧文件往返、宿主构建；修改通用FlowEditor则跑其模块门禁。跨后端误差以规格/独立基准为准，不能用同一实现反算预期或盲目逐像素一致。
- 代表任务：颜色→掩膜→Blob/公差；匹配→定位→卡尺/几何→公差；统计→显式参数连接→检测；对齐→差分→缺陷；标定→坐标映射→物理尺寸；保存→关窗/关闭会话→重开/再执行。覆盖源失效、无候选、有效0、运行重入/关闭、节点删除和迟到结果。
- 每阶段执行后至少两相关官方来源/作品对照，更新达到/差距/待证和验收记录；真实SDK与文档核查、合成精度与现场节拍分开。当前不承诺无实图/无授权条件下的日历工期、计量精度或统一耗时。
- R0代码按0.0整页复用要求收敛，物理UI/主题跨屏与SDK授权仍待验收；R1–R7待实施。当前门禁451/417/414及64属性定向通过，不抵扣原十项残项。

### 0.6 本轮资料核查与已识别风险

2026-10-24 19:05 +08:00规划时直接读取HALCON2311 gray_histo、dyn_threshold、measure_pairs与VisionPro10 2D清单均HTTP200；快照%TEMP%/vision-dual-backend-plan-reference/，内容/链接及SHA256见TOOL_EXPANSION_PLAN新双后端节。Exa第二次查询达到服务限流，未改配置/安装依赖；直接官方读取仍成功。本轮仅写计划，无测试/构建，也没有竞品实机或HALCON运行证据。

特别注意：gray_histo会把uint2等非byte图归一化到256级，且忽略预设domain、使用显式Regions；dyn_threshold不等同于OpenCV局部阈值的偏置/边界/比较规则；这两项是补HALCON工具前必须解决的语义差异，不是改个类名即可交付。

---

本轮交付IntensityTransform（Mono8线性/Gamma/显式本帧归一化）、AdaptiveThreshold（Mean/Gaussian、整数C、正反向）、ColorSegment（RGB/HSV范围与H环绕，公共输出Mono8掩膜）。后台快照/原注册/原.flow与结果呈现复用；两条新组合有独立面积/统计值、保存关闭重开及失败清旧证据。E1六项首期节点全部在目录，但无Otsu、预处理搜索ROI或图形直方图，不将其称为全部规划完成。执行后18:30–18:32重新核实Aurora ImageProcessing、MERLIC Transform Color Space及OpenCV4.11相关源码；4.11/4.x文档HTTP403单列为未读，不用5.0/4.13搜索摘要冒充当前后端文档。详见扩充计划V1.2及EDITOR_REFINEMENT。

V1.21首批执行记录（历史）：新增ImageConvert/ImageFilter/GrayStatistics，复用原参数/ROI/结果/后台快照、单一注册与通用.flow执行。彩色/16位→转换→滤波→阈值→Blob/统计、保存关闭重开及失效清理有实际回归；灰度统计另验证原模板匹配的搜索ROI平移跟随。直方图目前是可分档计数/文本，不是图形曲线。完整能力/边界见[工具扩充计划V1.1](TOOL_EXPANSION_PLAN.md)，实际命令/红绿证据见../../Tests/EDITOR_REFINEMENT.md。2026-10-24 17:53 +08:00在最终测试/构建后重核Aurora ImageProcessing及Cognex Histogram官方页面，均HTTP200。未进入测量工具跟随、OCR/标定或授权硬件验收。

V1.20规划轮记录（历史）：新增范围见[视觉工具扩充计划](TOOL_EXPANSION_PLAN.md)：核查19个实际后端条目及5个遗留接入候选，对照VisionPro/MERLIC/Aurora官方资料；分E0可组合性、E1基础6项、E2测量判定4项、E3差分/坐标/标定4项、E4OCR/印字2项。优先T01图像转换、T02滤波、T05灰度统计；约29/33/35项只是分期范围预算，不以后端副本或空节点凑数。E0包含原P7测量跟随建议的待实施范围，不表示已执行P7或已授权全部扩充。**本轮仅调研/写计划，未改运行代码，未执行测试/构建；历史门禁不抵扣新增工具验收。**

V1.19代码轮执行记录（历史）：仅调整布局为“左侧工具箱 → 流程画布 → 图像/参数编辑器”，展开方向同步向右，保留窄窗口收起、搜索、添加/拖放及运行守卫。原有布局测试先按左侧要求失败，再修改XAML后通过；工具箱/P6/组合定向Passed 24, Failed 0, Skipped 0，见%TEMP%/vision-toolbox-left-verified.log；显式宿主增量构建0警告/0错误，vision-toolbox-left-build.log。本轮未改算法、生命周期或通用FlowEditor，未重跑全量。已查看重新生成的processing-branches.png确认左侧布局。2026-10-24 16:55 +08:00执行后重新访问Aurora FirstProgram与OpenPnP CvPipelineEditor均HTTP200，事实/取舍沿用V1.18对照（工具选择/连接/配置与阶段结果，不以文档决定左右方位）；快照%TEMP%/vision-toolbox-left-reference/，SHA256与下方V1.18两来源相同。未执行物理鼠标/OLE、跨屏或授权SDK验收。

## 1. 目标、事实与范围

目标是完成用户提出的十项要求，形成可保存、可编辑、可执行、支持定位跟随的多后端视觉任务软件；不是复制 VisionPro 的全部功能，也不是继续增加互不相连的演示入口。

当前事实：自定义显示、属性特性生成、三个基础属性页、单工具工程、两类基础匹配已有基础；P1新增Vision.FlowNode，Workbench任务入口已直接内嵌通用FlowChartView并借用轻量单工具编辑会话。P2任务持久化和显式输入已落地，P3定位到Blob主链已落地，P4统一训练取样图已进入同一画布；主任务链全部测量工具跟随仍未完成；Controls遗留公开任务/节点的兼容依赖尚未移除。ECC审查发现的三个问题及相关Provider清理遗漏已在P0修复。6项回归均有失败后转绿证据；P0修复轮逻辑380、WPF310项非Runtime用例通过，Workbench增量构建0错误；P0宿主补验轮仅补测试，WPF全量312项通过；本轮P1实施结果见P1节。P0通过离线自动化及宿主关闭集成门禁；真实鼠标/显示器及授权SDK验收仍单列，不代表整个计划完成。

前轮逻辑380项、WPF304项通过及宿主构建结果只是历史基线，不是本计划的验收结果。实际执行证据继续记入 `../../Tests/EDITOR_REFINEMENT.md`，本文件只维护当前计划和计划修订，不为每阶段另建一套报告。

### 目标主线

```text
任务文档：持久工具实例、配置、输入输出连接、定位来源
                 ↓
已有执行器：本次图像/参数/ROI/坐标快照 → 原后端工具 → 原始结果
                 ↓
呈现转换：图像记录、几何、明细、状态和结果身份
                 ↓
同一个可借用编辑器：操作栏 + 自定义画布 + 参数/ROI/结果 + 能力附加内容
```

### 用户确认的目标架构

不新建 `Vision.FlowEditor`。Workbench直接组合现有通用FlowEditor与视觉控件；新增 `Vision.FlowNode` 作为视觉工具到流程的适配模块，不承接算法实现。

```text
Vision.Workbench（完整视觉应用宿主，负责组合与注册）
 ├─ FlowEditor.Controls → FlowEditor.Runtime / FlowEditor.Core
 ├─ Vision.FlowNode → 原视觉工具、Vision.Core、通用流程契约
 └─ Vision.Controls → 图像/参数/ROI/训练/结果UI及必要呈现适配

原视觉工具与后端全部保留：
 Vision.Tools.Halcon  → HALCON算法、模型、SDK资源
 Vision.Tools.OpenCv  → OpenCV算法、模型、SDK资源
 Vision.Tools.AI      → 分类/检测/分割/异常检测工具逻辑
   → IAIProvider
      → Vision.AI.Halcon / Vision.AI.Aiditron / Vision.AI.Invi
工具及Provider依赖Vision.Core，不反向依赖视觉节点、编辑器或Workbench。
```

- `Vision.Controls` 不承担流程调度，不依赖 `Vision.FlowNode`；任务/节点适配迁出后解除目前对FlowEditor的直接流程集成依赖。
- `Vision.FlowNode` 调用原工具，不复制后端算法、不创建第二份参数，也不拥有编辑窗口。视觉编辑器的选择和组合由宿主通过通用扩展入口完成。
- `Vision.Workbench` 可以是完整视觉软件，但不能将算法、任务文档规则或调度逻辑搬进壳代码。
- 通用FlowEditor负责画布、连线、文档、调度、调试和停止；视觉特有配置通过节点/文档扩展承载，不添加厂商判断。
- 当前通用NodeModel位于FlowEditor.Controls。P1先确认适配路径；必要的现有Controls依赖必须如实记录，不把新节点项目仅凭命名称为无WPF。禁止顺手迁移公共基类或重写流程框架。
- 本图是目标，不是当前已完成结构。仅授权增加必要的Vision.FlowNode；不顺带增加Vision.FlowEditor、Vision.Runtime或新的执行引擎。

### 不变的边界

- Core 保持无 WPF、无厂商 SDK；算法、模型和厂商资源留在 Tools/AI 对应后端。
- 继续使用现有 FlowEditor 执行能力，不另建视觉调度器或通用插件框架。
- 原工具、参数 DTO、ROI、编辑器、模型训练入口和结果选择机制优先复用，不另造副本。
- 单工具旧路由、`.visionproj` v1/v2、类型身份和有效兼容入口保持；任务文件格式单独明确版本，不偷偷改写旧格式。
- 所有权不同的节点不能只为减少类数强行合并；应共用执行准备和坐标语义，避免功能分叉。
- 普通参数通过特性生成；ROI 手势、模型训练和异构结果可以使用共享专用组件，不要求每种交互都塞入 PropertyGrid。
- 不默认安装 Runtime、加载生产模型、连接相机/设备或运行生产流程；授权 SDK 验收单列。
- 不为目录整齐新增项目、批量改名、重写全部注释或引入 Docking 依赖。

## 2. 强制执行闭环

**每个阶段都执行以下步骤，不能直接跳到下一阶段：**

1. **实施前复核**：重读实际文件和工作区改动，核查现有可复用机制；写清本阶段范围与非目标。
2. **最小实施**：缺陷先取得失败回归；新增行为以用户可观察结果为验收，不为内部细节堆测试。
3. **本地验证**：按影响运行逻辑、WPF/STA、配置兼容和宿主构建；记录命令、摘要、失败、超时及未验收项。
4. **执行后联网对照**：用实际实现、运行证据和可用的离线截图，对照至少两个相关作品/官方来源；不能只把计划文字和宣传图比较。
5. **更新计划**：记录“达到/存在差距/不适用/待证”，对每个差距决定“采纳/暂缓/不采纳”及理由；更新剩余阶段、验收标准、风险和版本号。
6. **阶段门禁**：阻断缺陷先修复并复测；新增重大范围、依赖、破坏性兼容变化或硬件操作先征求用户确认。阶段门禁通过后再进入下一阶段。

阶段后的联网对照是交付的一部分，不是可选调研。外部案例的新功能不能自动变成本项目需求。

### 外部资料使用规则

- 优先官方文档和作品源码，记录实际访问日期、URL、版本/分支和具体对比点。
- 页面返回429/404、只有搜索摘要或无法打开时，记为“待证”，不得声称已完成该项对比。可先使用其他可核实来源；若缺失内容影响本阶段关键设计，相关决策保持阻塞。
- 只发送公开技术问题；不上传仓库源码、生产图像、配置、日志、DLL或许可证。
- 借鉴职责和交互，不复制别人的实现/XAML；尊重后端差异，不追求数值阈值或结果逐像素相同。

## 3. 阶段计划与门禁

### P0：修复已知生命周期与交互问题

状态：已完成代码修复、离线自动化验证、宿主关闭集成及执行后联网对照（2026-10-24）。6项红绿回归加2项Closing集成用例，后者通过真实WorkbenchLifetime/CloseRegionAsync验证活动页等待及失败缓存页清理；本轮定向2项、WPF全量312项通过。P0宿主补验轮未改生产代码，未重复运行逻辑或宿主构建，二者当时保留修复轮380项及0错误证据；本轮P1已重新验证，见P1节。物理鼠标、真实跨屏和授权SDK不是本阶段自动化通过结论。

**范围**：`VisionTaskViewModel`、`VisionTaskToolNode`、`ToolCardWorkspace/ToolCardViewModel`、`VisionTaskView`及现有测试中必要的最小变更。

**工作**：

- 一个节点释放抛异常时，仍清理其余实例，并保证必要的运行等待发生；错误保留并可观察，不能吞掉。
- 关闭时不仅等待整任务，也覆盖编辑器单工具按钮发起的实际执行；不把取消等待当成 SDK 已停止。
- 执行中只禁用修改和重入操作，保留画布平移、缩放和只读查看。

**验收**：

- 释放失败用例证明后续实例仍恰好释放一次，错误未丢失。
- 分别从“执行任务”和“执行当前工具”发起后台调用，关闭均等待实际结束；迟到结果不回填、不漏清理。
- 运行中实际画布浏览仍可用，参数/ROI/输入修改及重复执行不可用。
- 原取消、失败、导航和所有权回归保持。
- 必须覆盖真实宿主Closing与Region清理链，不仅直接调用任务VM.DisposeAsync；失败的非活动缓存页不能跳过活动页等待。

**执行后对比**：Aurora 的预览操作与运行状态；OpenPnP 的阶段结果和资源生命周期。根据实测更新关闭策略与交互验收，而不是照搬线程实现。

**非目标**：任务保存、算法升级、统一节点重写。

### P1：建立Vision.FlowNode，Workbench直接复用通用FlowEditor

状态：本轮先完成P1复核与资源退役修复：新增2项移除空闲节点红绿回归、1项运行中移除回归，逻辑381/WPF322全量通过。P1新主路径门禁通过，允许进入P2；这不代表Controls历史流程引用已清零。旧公开类型的保留策略已按实际消费者核对并受新增精确边界守卫约束，作为P6兼容债务明示保留，未删除类型或增加兼容程序集。以下V1.5证据为历史实施轮。实施前P0的8项回归全部通过（vision-p1-p0-recheck.log）。最终定向24、逻辑380、Vision WPF319、通用FlowEditor相关24项通过；Workbench增量构建0警告/0错误。新增7项P1用例中，前置取消保留旧结果取得独立失败后修复；未将编译错误或非法测试拓扑称为产品红绿证据。

实施前确定并在新主路径落地的所有权表（旧兼容路径仍单列）：

| 对象 | 创建者/拥有者 | 借用者 | 释放与停止 |
|---|---|---|---|
| 原工具、原参数DTO | 条目工厂创建；单工具会话经原Card唯一拥有工具，DTO不复制 | 节点及共享参数编辑器 | 节点所属流程关闭时异步归还会话；等实际Card执行结束 |
| Provider、训练/结果适配 | 原PreparedProjectSelection准备；单工具会话拥有 | 原工具和借用编辑器 | 工具清理失败仍尝试Provider及其余适配；聚合错误 |
| 本次结果 | 原ToolCardWorkspace拥有 | 节点输出端口与结果视图 | 重跑/失效/关闭由原工作区释放，不由节点二次Dispose |
| 编辑会话、ROI集合 | 轻量单工具会话拥有，不含浏览目录切换/工程文件协调 | Workbench内嵌同一个VisionToolEditorView | 视图卸载只退订，不释放运行资源 |
| 节点与已移出画布的撤销实例 | 流程所有者持有至流程关闭 | 通用FlowEditor画布/执行器 | 停止原命令后异步遍历关闭，单个失败不短路 |

核查结论：NodeModel仍在FlowEditor.Controls，新节点真实依赖WPF；INodeEditorHandler/Launcher可复用；LoadNodeModel可接收宿主创建的节点；目录只反射元数据；文档mapper目前不能恢复按条目配置的通用视觉节点，任务保存/复制恢复不得冒充P1已支持。旧Controls公开任务/节点类型及FlowNodes视觉节点有仓内测试/宿主消费者，暂保留兼容，不为去掉一条引用破坏类型身份；Controls遗留FlowEditor引用仍需如实列为收口差距。

**已实施**：

- Core新增无WPF的IVisionToolSession执行租约和SearchRegionPreparation；新主路径与旧程序化节点共用基准ROI刚体准备，不复制算法。
- Vision.FlowNode.VisionToolNode接管会话，借用结果及图像，声明现有NodeTypeId/端口；只引用Core/FlowEditor.Controls，明确仍依赖WPF。
- Controls新增VisionToolInstanceWorkspace，复用PreparedProjectSelection、原Card、DTO、ROI、模型/Provider/结果适配；不再为新节点创建完整BrowserWorkspace或工程协调。
- Workbench任务入口改用VisionFlowView：直接组合通用FlowChartView及同一个VisionToolEditorView，INodeEditorHandler仅选择已有实例。首节点为入口，后续Next由用户连接；运行/停止仍使用原FlowChartViewModel/引擎。
- 通用FlowEditor新增默认开启的SupportsDocumentEditing宿主能力，视觉P1关闭保存/打开/复制恢复/撤销重做；快捷键遵守CanExecute。没有虚假任务保存按钮，没有提前实现P2文件格式。
- 新主路径覆盖双执行入口关闭、失败缓存页与Provider清理、运行中切实例只读浏览、实际通用连线渲染及前置取消清旧输出。两个96-DPI合成截图在Tests/obj隔离目录，不是物理鼠标/跨屏验收。

**P1复核结果及显式保留项**：

1. 对旧Controls公开VisionTaskViewModel/VisionTaskToolNode/VisionToolFlowNode及旧FlowNodes消费者确定兼容壳/弃用窗口；目前保留旧类型、导航和执行行为，Controls仍有FlowEditor引用。不能同时声称这些旧CLR身份无损且流程依赖已经清零；不能为去引用擅自破坏API或增加平行兼容工程。
2. 已修复移除实例的延迟回收：通过通用Chart.Nodes集合变化采用/退役节点，空闲立即回收，实际调用未完成时登记异步退役并锁修改，不回填迟到结果；失败实时显示且关闭仍聚合上报。2项先红后绿（DisposeCount期望1/实际0），运行中移除另有通过回归。所有权表中“移出画布保留至关闭”是V1.5历史策略，现已替换。
3. 已核对的仓内消费者包括旧路由注册/WorkbenchComposition及VisionTask/VisionToolFlowNode/P0测试；旧FlowNodes HALCON节点亦保留。ControlsDependencyTests精确约束只有三个既有公开类型引入FlowEditor，且Controls不得引用Vision.FlowNode。未经外部兼容确认不移动或删除旧CLR类型；该受控兼容保留项交P6，不再要求破坏API来通过P1主路径门禁。物理拖线/鼠标/跨屏及授权SDK仍单列，不以当前截图或fake冒充。

**范围**：必要的新Vision.FlowNode项目、现有节点/执行适配、原编辑会话、Workbench装配与项目引用。保留全部Tools/AI后端，不新建Vision.FlowEditor。

**工作**：

- 先核查通用NodeModel、节点工厂、专用编辑器、文档映射和程序集发现的扩展入口，再确定节点迁移清单及最小引用。
- Vision.FlowNode承接视觉节点、工具实例配置和运行适配，调用既有IImageTool及后端能力；不把HALCON/OpenCV/AI算法迁入节点。
- 让Workbench直接使用FlowEditor控件，通过现有节点编辑扩展入口组合VisionToolEditorView；不逐工具新建参数页面，不把流程业务塞进Workbench。
- 明确实例ID、工具类型身份与显示名称，原参数/模型/结果拥有者唯一；编辑器借用节点实例，解除每个步骤包装完整BrowserWorkspace的过渡耦合。
- 收口主任务链与程序化节点的输入准备和定位语义，保留必要的SDK线程/快照差异；不强行合并所有权不同的对象。
- 根据P0执行后对照，迁移前先写清工具、Provider、结果和编辑视图的所有权表；节点资源由任务运行所有者管理，预览/编辑视图只借用，不能在视图卸载时释放算法资源。
- 迁移当前Controls中的视觉流程适配后，移除由本次迁移造成的重复路径及不再需要的项目引用；旧FlowNodes视觉节点、旧类型/导航/序列化身份按消费者保留或转发，不批量破坏。

**验收**：

- Workbench实际使用通用FlowEditor操作视觉节点，选择节点打开/组合同一个视觉编辑器，无Vision.FlowEditor项目或第二套调度器。
- HALCON/OpenCV/AI各自模块、能力及模型生命周期保持；目录声明不加载SDK或模型，旧工具配置和算法回归不变。
- 两个同类节点的参数、ROI、状态独立；切换视图不重建工具、不复制DTO、不释放运行实例。
- 当前工具与流程执行使用同一原工具和输入/定位准备，不再一条有编辑、另一条有跟随。
- P0的两种执行入口关闭等待、真实Closing/Region缓存页清理、释放失败后继续清理、Provider归还与运行中只读浏览回归随迁移保留；不能仅新节点路径通过、旧编辑按钮或宿主关闭再次漏等。
- Core/Tools/AI不反向依赖节点/UI；Vision.Controls不承担流程调度、不依赖Vision.FlowNode。当前NodeModel带来的WPF边界有明确说明与验证。

**执行后对比**：MERLIC工具实例/连接、OpenPnP阶段与通用编辑器、可核实的VisionPro节点/工具编辑组织。核查复用边界，而非以项目数量增加作为完成证据。

**非目标**：新建Vision.FlowEditor或Vision.Runtime，重写通用FlowEditor、算法后端，强行迁移公共基类或删除仍被消费的旧契约。

### P2：任务文档、保存恢复与显式输入输出绑定

状态：P2复核门禁已通过：本轮通用FlowEditor全量414/414、逻辑381、WPF336通过。修正过期解决方案名称引用；新增有源码依据的FlowEditor/ARCHITECTURE.md作为当前维护入口，解决方案与测试继续验证所有项目/文档链接真实存在，不补造缺失历史报告。零节点任务输入状态先红后绿，增加通用DocumentProperties及视觉状态校验包装器；原节点/文件/执行机制复用。大图多实例往返另有验证，资产去重仍明确未实现而非门禁阻断。以下V1.7失败记录保留为历史事实。

**已实施及格式边界**：

- 使用原通用.flow根文档1.0、INodeFactory、FlowDocumentMapper、JsonFlowDocumentStore及原执行器；新增INodeDocumentState严格状态扩展和VisionNodeFactory，只对目录已知视觉配置物化会话，不建立新视觉存储/调度引擎。
- 节点Properties.Configuration为视觉配置v1：原工具目录身份、原参数JSON、Provider、ROI/选择、基准/静态位姿、原始输入像素（包括stride/format/source/frame/timestamp/hash）、取样图/训练参数及模型引用。节点ID、端口ID/连接及选择保持；不保存历史结果或原生句柄，不自动运行/加载/训练。
- 输入图像按节点配置保存托管快照，不把本次上游输出改写为任务原图。此实现尚未去重图像资产，大图/多实例文件体积与零节点任务独立图像状态仍是后续收口项，不宣称已有统一资产包。
- 通过IOutputImageResult与条目显式能力声明输出图像，当前Crop/Threshold/Morphology可提供；不按名字/Backend猜测。图像来源下拉只修改原通用连接，复用PortValueTransfer和SyncInputValues；当前工具与流程运行共用准备回调。Next不自动替换输入，来源无本次有效输出则失败，不回退原图。定位来源选择界面仍P3，不把存储基准位姿当完整主链定位。
- 打开时先完整准备候选节点并校验连接，再替换画布；未知工具、坏参数/像素hash、端口不存在或类型不匹配均保旧。存储先序列化再同目录临时写入/替换，序列化失败或目标锁定不截断原文件，不承诺任意文件系统的断电恢复。
- 复制/粘贴配置冻结为值快照，参数和ROI独立；模型引用缺失明确提示，恢复取样不调用SDK加载/训练。旧.visionproj v1/v2继续由原单工具入口打开；任务加载器明确拒绝把单工具JSON当空流程。
- 实测真实离线OpenCV Crop→Blob：未绑定面积100，显式选择Crop输出后面积12，保存重开及当前工具按钮仍12；清除来源结果后下游失败且不回退原图；切回任务输入恢复100。真实组合页验证来源选择加载后不被控件重置，当前执行尺寸为4×3而配置原图保持10×10；96-DPI截图在Tests/obj隔离目录。

**V1.7门禁记录及本轮处理**：

1. FlowEditor.Workbench.Tests.P3NamingGovernanceTests的Solutions_ReferenceExistingProjects_AfterRename、NoInfrastructureCatchAll_InSolutions找不到仓库中的旧名称Software-Framework.sln；ArchitectureDocuments_ReferenceCentralDocumentation找不到Documentation/Refactoring/Software-Architecture-Analysis-and-Refactoring-Plan-2026-10-03.md。当前根解决方案是Software Frame.sln；未擅自重命名用户解决方案或补造中心文档。必须核对真实布局/资料来源并复测，不得把411/414说成全量通过。
2. 图像快照目前逐实例保存；需补大图体积、零节点任务输入状态以及输入来源切换/上下游即时失效的进一步产品验收。定位来源UI及匹配姿态消费仍待P3；不能以已有端口和基准位姿抵扣。
3. 真实HALCON/AI Runtime、物理拖线/鼠标/跨屏以及跨机模型引用仍未授权验收；OpenCV合成图真实算法证据不替代这些项。

本轮未改旧工具算法公式、包版本、旧单工具格式或生产启动行为。

**范围**：现有FlowEditor文档/端口能力、Vision.FlowNode的视觉配置扩展、原单工具工程协调和宿主文件适配；不建立另一套视觉流程文档引擎。

**工作**：

- 优先使用通用FlowEditor的文档、节点标识、连接和恢复能力，只增加必要的视觉工具参数/ROI/模型引用扩展；先明确版本及旧工程导入策略，不另写平行任务存储。
- 保存实例顺序/连接、实例ID、工具身份、参数、ROI、输入来源及定位来源；明确模型引用与图像快照的归属和恢复策略。
- 建立显式数据绑定，不再只有 Next 控制流：允许选择任务输入或某个工具已声明的输出；不自动把处理结果图替换所有下游输入。
- 导入先完整准备候选状态，校验成功才替换；损坏文件、未知工具或无效连接不能破坏当前任务。打开不自动执行或推理。
- 明确并保持旧单工具工程的读取/导入路径，不将其直接冒充多工具任务。

**验收**：

- 多个同类实例保存重开后ID、参数、ROI、连接和选择语义一致；无意外工具创建、SDK启动或旧结果恢复。
- 用实际处理输出连接检测工具，验证下游得到选定输入，而不是悄悄继续使用原图。
- 缺图像/模型、未知类型、断开连接、类型不匹配、取消文件选择、写入失败均有明确行为。
- 文件失败保旧、版本/历史配置兼容、图像格式/stride/像素与单位保持有回归证据。

**执行后对比**：MERLIC 的工具连接和任务参数/结果；OpenPnP 的阶段配置与结果关联；Aurora 输入输出配置。比较的是实际保存重开与数据流，不只是连线外观。

**非目标**：先做任意图画布、复杂布局持久化或完整外部插件扫描。

### P3：主任务链的定位跟随与仿射能力

状态：P3刚体定位主闭环已实施，最终逻辑385/WPF349非Runtime项通过，0失败/0跳过，Workbench增量构建0警告/0错误。P3的全部工具适配、任意帧局部坐标编辑及授权SDK验收尚未完成，不能标记整个P3/第8项要求完成，也未进入P4。能力与证据见FIXTURE_CAPABILITIES.md。

实施边界：复用匹配原结果，新增显式定位结果能力（位置/角度/分数/真实缩放）、匹配索引选择、来源连接和“设为基准”；目标仅限声明搜索ROI能力的工具。NodeModel/会话/原Card仍是唯一执行链。先完成OpenCV灰度匹配的平移与HALCON形状结果的刚体能力契约，不声称OpenCV固定方向匹配能测旋转；单位缩放以外明确拒绝。当前/基准图像必须在同一像素坐标系；不同图像/缩放/缺结果/索引过界不可回退旧位姿。ROI仍为基准所有，执行几何作为只读派生叠加，视图不释放工具或结果。

所有权沿P1/P2表：源结果由源Card拥有，定位候选/端口只借用；目标从基准ROI每次派生，不改源结果或原ROI；上游失效、换来源/匹配索引/基准时撤下依赖结果，实际调用仍遵守P0关闭及revision迟到隔离。

**本轮实际交付**：

- 原OpenCV TemplateMatchResult/HALCON ShapeMatchResult显式实现IFixtureResult，候选保持原顺序/位置/分数；OpenCV明确不提供角度估计，HALCON保留真实尺度并由刚体消费方拒绝非单位尺度，不修改匹配算法。
- 主任务页新增定位来源、从0起匹配索引、设为基准、基准X/Y/角度；仍修改原通用连接并使用PortValueTransfer，两个执行入口共用原准备回调。换候选须重新设基准，配置/端口/基准/选择随原.flow恢复。
- 来源/目标必须是同一输入图像坐标：记录执行准备时的尺寸/格式/stride/frame/timestamp/像素指纹，无结果/失败/越界/非有限位姿/尺度不支持/旧帧/像素改写均拒绝，不回退旧位姿。
- 上游结果或连接失效撤下依赖结果；后台目标失效时撤销搜索域叠加并依原revision丢弃/释放迟到结果。源结果由源Card拥有，节点候选端口只借用，不二次释放。
- 目标基准ROI保持原对象/原参数身份，每次重新派生；紫色只读虚线为实际输入坐标的本次搜索域，不参与ROI手势。Blob结果仍用原图像坐标，不把坐标系混画。
- 真实离线OpenCV模板匹配→Blob，通过实际组合页选择来源/点击绑定命令、换位置、手动按钮、保存重开验证无漂移；受控HALCON原结果消费验证+90/-90/180°，不调用HALCON运行库。多匹配第二候选选择、失效及迟到场景均有回归。
- 正交角回归暴露4×2 ROI应含8像素却含9像素；独立Core回归先失败（±90°），以精确正交角SinCos修复Pose2D和SearchRegion，仅对恰为90°整数倍处理，一般角度不做容差近似。后台上游失效的派生叠加残留亦先红后绿。

**仍需完成/边界**：

1. FindLine/FindCircle等尚未声明搜索ROI能力，未接主链定位跟随；HALCON Blob/ShapeMatch真实SDK、训练/多形状组合需授权，不以受控结果/OpenCV抵扣。
2. 当前编辑器仍编辑基准ROI并显示只读本次派生ROI，尚非Aurora式任意帧自动对齐的局部坐标编辑；图像预处理/裁剪后与原图之间坐标链不自动推断，目前严格同输入约束。
3. 缩放/非均匀缩放/镜像与世界单位换算未明确或未实现，明确拒绝而非近似；物理拖线/跨屏/全部主题DPI尚未验收。
4. P2图像资产去重仍未实现；2 MiB图像/两个节点的文档实测8,394,955字节（含任务及节点快照），不得描述为压缩资产包。零节点输入已正确恢复，扩展状态校验在原存储包装器执行。

**范围**：Vision.FlowNode中的输入/定位绑定和执行准备，已有AffineTransform2D、CoordinateChain、ROI/卡尺几何、匹配结果与原后端工具。计算语义留在Core/Tools，节点适配不复制算法。

**工作**：

- 建立工具能力矩阵：明确每个工具接受哪些几何、坐标和变换，哪些不适用，哪些必须拒绝；不能把“所有工具”理解为给标定等不适用工具强塞跟随。
- 第一闭环必须从主界面完成：“匹配/定位工具 → 选定定位结果 → 下游检测或测量”。不能再只验证未进入主任务链的帮助类。
- 明确基准坐标、本帧位姿、图像身份、像素坐标与世界/机台坐标；每次从基准几何生成执行快照，不累计改写 ROI。
- 对多匹配显式选择匹配对象/规则；定位失败、无匹配或来源过期时，下游不可使用旧姿态。
- 先完成平移/旋转；缩放、镜像及一般仿射需在算法、单位、形状表达和后端支持上逐项确定。Pose2D 不冒充完整仿射；圆、矩形、卡尺在非均匀缩放/镜像下不能静默近似或假装支持。
- 输出叠加回正确图像坐标；世界测量值不直接拿来画图，像素长度不冒充标定长度。

**验收**：

- 主任务页通过脱敏/合成多位置、多角度图运行同一任务，搜索域和结果正确跟随，基准参数不变、重复运行无漂移。
- 同时验证实际执行几何与显示几何，不只看青色框是否移动。
- 图像切换、定位失败、多匹配切换及旧结果到达均不能污染当前任务。
- 每个工具在能力矩阵中有“支持且有证据/不适用/明确不支持/待授权验证”的状态；不能把待证项算作第8项完成。
- 缩放/镜像未确定或未实现时，保留为明确未完成项，不以已有矩阵类抵扣。

**执行后对比**：重点对照 Aurora Local Coordinate Systems 的 alignment 输入、局部编辑、绝对坐标输出和叠加规则；可访问时再核查 VisionPro Fixture。根据实际差距更新能力矩阵和剩余算法工作。

### P4：统一图像记录、操作栏、属性能力与结果呈现

状态：本轮独立显示/呈现范围完成并通过离线门禁；P3测量/局部编辑与授权、物理交互验收保留。按本轮用户授权推进独立子项，不宣称P3整体门禁已全部完成，不自动进入P5。

**实施前P3复核**：重读来源候选、同图像校验、基准与失效传播；定向13/13通过（%TEMP%/vision-p3-review.log），最终WPF全量仍覆盖这13项。不改变刚体能力矩阵，不将托管HALCON结果消费冒充SDK训练/匹配。

**本轮P4实际交付**：

- 原ImageViewKind末尾追加Training，保持Input/Roi/Result原值；同一VisionImagePanel/Viewport显示输入、ROI、结果和真实模板取样图。训练视图仅显式模板能力工具可见，无数据禁用；清取样后显示明确空态，不回退输入冒充样本；切无能力工具回输入。
- TemplateEditorViewModel暴露借用的原TemplateImage，刷新/清除/模型加载与切工具通知同一路径；编辑器订阅可退订。工具仍拥有取样/模型，画布和借用会话不释放它们。不复制训练数据成第二份参数或增加模型引擎。
- 训练图是局部像素坐标，禁止输入ROI手势、输入结果拾取与派生搜索域叠加，即使与输入尺寸完全相同。缩放/平移/1:1及原像素PNG/TIFF导出仍可用，不修改输入/参数/结果身份；只读运行模式仍可浏览。
- 输出图使用已有IOutputImageResult，不再按Crop/Threshold/Morphology具体类型分支。第三方托管输出契约有回归；并不宣称提供第三方插件自动发现/所有异构结果自动绘制。
- 后端结果绘制从Viewport移至Presentation/Rendering/VisionResultDrawing，保留原结果、选择ID/对象身份、轮廓缓存、颜色、层次和坐标。画布不包含Tools/AI结果类型分支，新增守卫。标量表/摘要/元数据参数链及三个基础页继续沿用，不增加巨大通用DTO或逐工具页面。
- 图像记录/空态与当前实例操作状态在同一行分列截断，完整提示可查看。修复最初新增信息行导致绘制时画布高度变化；未放宽原高度断言。原工具/任务打开、保存、执行归属及原像素导出语义保持；本轮未新增截图命令，验收截图为独立命名的测试产物。

**证据**：新增P4 9项（真实OpenCV模板取样/导出、第三方输出契约、切工具、4组四视图主题/DPI重载、2种Mono16/stride导出契约），加1项画布呈现边界守卫。最终逻辑386/386、WPF358/358，0失败/0跳过；Workbench显式增量构建0警告/0错误。日志vision-p4-release-{logic,wpf,host}.log，截图/像素图在Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p4-acceptance/。Mono16取样为复用fake的托管显示契约，不是HALCON SDK训练；OpenCV模板仅接受Mono8。真实物理跨屏/鼠标及授权SDK仍未验收。

**剩余/暂缓**：P3未适配的测量与局部编辑不由P4抵扣；HALCON/AI真实模型和结果留授权环境；完整十项终验仍由P5/P6及P3剩余工作闭合。任意窗口Docking、拖任意端口预览、显示gamma/归一化及新增截图产品命令不扩入本轮。

**范围**：原 VisionToolEditorView、VisionImagePanel/Toolbar/Viewport、ToolPropertyAttribute/属性编辑器、模板编辑与现有结果模型。

**工作**：

- 同一自绘画布支持原图、ROI、训练图、结果图；训练图来自真实取样/训练数据，不伪造图片或把局部图当原图编辑。
- 明确任务级与当前工具级打开/保存/执行，图像导入与保存原图；若提供视口截图则独立命名，不混淆语义。
- 保留参数/ROI/结果三个基础页，依据显式能力增加训练/模型等内容；普通参数、条件可见性、校验和必要集合编辑使用现有元数据链。
- 收口结果到图像几何、表格和摘要的转换，继续使用原结果身份/选择模型；不把所有后端字段压成巨大通用 DTO。
- 底部状态区区分校验失败、运行失败、停止、模型未准备和正常完成，显示对应实例及真实耗时。
- 根据Aurora预览原则，显示变换与算法参数/输入分离；任务运行只锁修改，不锁缩放/平移。任意多窗口预览、Docking和拖拽任意端口预览暂缓，不因对标自动加入本期。

**验收**：

- 四类图像对可用工具可切换；无对应数据时按能力隐藏/禁用或明确提示，不显示伪造结果。
- 修改普通工具参数不需要新增该工具专属参数页面；新增同类结果不要求修改通用画布算法分支。
- 复杂 ROI、训练交互和异构结果表仍正确工作，不以减少 XAML 为由删除必要行为。
- 原像素保存、截图区分、位深、原点、结果互选、加载卸载、窄窗口和主题有实际验证。

**执行后对比**：Aurora Preview/Data Presenting、模板训练编辑；OpenPnP 参数描述与通用编辑器；MERLIC 参数/ROI操作。用实际页面截图和操作结果对照，避免只复制布局。

### P5：匹配与AI工具的可交付闭环

状态：本轮离线可执行范围完成并通过门禁；P5整体仍有HALCON/AI授权SDK及实图待验收，不自动进入P6。P4定向复核12/12通过（%TEMP%/vision-p4-review.log），四视图、局部坐标隔离与绘制缓存保持；P3测量/任意帧局部编辑等残项不销项。

**本轮实际交付**：

- 真实OpenCV取样→保存.vtemplate→清除后明确执行失败→显式加载→双匹配原始分数排序→选第二候选/设基准→保存.flow→移开引用文件仍从内嵌取样重开→换图像平移→定位到Blob；面积9、框(52,30,3,3)，基准ROI不改。坏模型文件拒绝，原取样与引用保留，但来源/下游旧结果撤下。合成图/96-DPI截图不是实图精度或授权SDK验收。
- 修复成功重新取样/训练/标脏/清除后旧ModelReferencePath残留；保存/加载成功记录新路径，失败或取消不伪装新模型成功。引用变化有通知，模板页显示模型状态和引用；OpenCV内嵌取样可用时不再错误提示为必须先加载模型。任务文件不打包外部原生模型、不恢复运行结果或句柄。
- 模板VM保留原三参构造，新增工作区提供的只读/忙态守卫，保护命令和直接Capture；模态文件选择返回后再次检查，不能在任务进入只读后加载/改模型。沿用原会话与文件引擎，不新增参数副本、SDK调度器或隐式模型加载。
- 四类AI沿原目录/工具/Provider覆盖路径与设备身份变化、显式加载、.flow重开不加载、结果失效与关闭。8项契约用fake，不安装Runtime/加载真实模型或启动GPU；每例3个会话对应3次CloseSession。
- 实际Provider的非默认设备请求不得被静默忽略：HALCON当前只实现默认或cpu；Aiditron/Invi当前只实现默认，非空指定明确拒绝。Invi拒绝发生在native manager创建前，有独立先红后绿证据；三家补无原生调用契约。原参数名/格式及默认行为保持，过去忽略的非默认字符串现在明确报错。
- 匹配/AI支持范围、文件关系、设备限制、所有权与授权待验见MODEL_CAPABILITIES.md；没有把AI模型加载称作训练，没有增加灰度旋转/缩放搜索或下游缩放/镜像能力。

**验证**：P5定向15/15、设备契约4/4；最终非Runtime逻辑390/390、WPF373/373，0失败/0跳过；宿主显式增量构建0警告/0错误。日志vision-p5-wpf-full.log、vision-p5-logic-full.log、vision-p5-host-verified.log。复合命令在240秒工具时限后超时，WPF与逻辑已成功、首次构建未取得结果；独立重跑构建通过，空的vision-p5-host-final.log不作为成功证据。失败/命令/截图详见EDITOR_REFINEMENT。

**仍待验收/后续**：HALCON真实训练、.vshape/.shm读写、独立合成定位/分数及AI三家真实推理/模型预处理/SDK释放必须在授权环境完成；既有RequiresRuntime未去Skip或执行。P3未适配测量/局部编辑仍阻塞完整第8项；模型引用未做相对路径迁移/外部资产打包，P2去重未实现。P6前须据十项矩阵分类残项，不能以390/373代替完整交付结论。

**范围**：HALCON ShapeMatch、OpenCV TemplateMatch、原 ITemplateTool/模型文件及AI工具/Provider。

**工作**：

- 两类已有匹配继续作为基础：HALCON形状、OpenCV灰度；明确各自训练、搜索范围、得分、姿态和后端特有能力。
- 补齐“取样/训练 → 模型有效性 → 保存加载 → 匹配 → 多结果选择 → 定位输出 → 下游消费”的实际闭环。
- 灰度匹配当前固定方向/尺寸应如实声明；若本期确定需要旋转/缩放搜索，将其作为真实算法子任务，先确定精度/速度目标再实施，不只增加参数。
- AI沿用分类/检测/分割/异常检测各自结果与Provider能力；明确模型加载、失效、设备与授权，不将推理模型加载称为训练。
- 明确模型文件与任务引用关系；任务打开不暗中加载原生模型或执行算法。

**验收**：

- 两类匹配分别有独立依据的合成定位/评分测试，失败/重训/损坏模型文件不伪装成功；实际原图精度另验。
- 模型与任务保存重开行为符合声明，定位结果可被P3主任务链消费。
- 四类AI通过fake覆盖契约与生命周期，授权环境另做真实推理；后端数值不承诺逐像素一致。
- 每个后端/工具的支持范围、限制及授权验收状态清晰可查。

**执行后对比**：Aurora EdgeModel/GrayModel 的训练与匹配闭环；可核实的 VisionPro 对应能力；MVTec 官方算法文档。差距按本期十项要求取舍，不无限扩展到竞品全功能。

### P6：Workbench主入口、清理与十项最终验收

状态：本轮可离线执行的入口/闭环/审计范围已完成；P6“十项最终验收”尚未全部通过，不能报告整个计划完成。P5主链15/15与设备契约4/4复核通过（vision-p5-review.log、vision-p5-device-review.log）。

**本轮实际交付**：

- MainWindowViewModel默认导航到既有VisionFlowView，仍直接组合通用FlowChartView与原VisionToolEditorView；单工具及旧任务导航保持。启动前不添加工具/加载模型；测试在其它导航前记录实际初始活动页，不用事后导航冒充启动验证。
- 任务页补“保存任务”，绑定原Chart.SaveCommand；首次选路径、后续保存原文件，不另建文档/执行器。工具按钮、任务按钮的模型/输入/执行归属仍分离；单工具项目入口兼容，不成为第二套任务主路径。
- 同一离线场景经过实际页面命令、原文件解码、ROI取样/模型保存清除加载、显式定位基准、换图执行、保存、Window.Closing→WorkbenchLifetime→CloseRegionAsync、创建新会话打开任务、再次执行。原会话已关闭，结果不复活；两次Blob面积9、框(34,18,3,3)，基准ROI保持原值。文件选择为可控回调，未物理点击系统对话框或鼠标拖线。
- 发现真实文件导入强制8位灰度，新增文件往返回归先失败（Mono16/Bgr24/Bgra32变Mono8）。改用Unchanged与按行托管像素拷贝，保留Mono8/Mono16/Bgr24/Bgra32；当前不支持的浮点格式明确拒绝。多数原OpenCV算法仍仅接受Mono8，导入成功不代表算法支持该格式，不能在文件边界隐式降位/转灰度。
- 兼容审计：图索引日期旧且三个兼容类型/新宿主路径均缺少记录，未据零匹配认定无消费者。实际App注册、WorkbenchComposition.CreateTask、MainWindow外观插槽及兼容测试仍消费旧类型/路由；精确三类型守卫保留。没有破坏性删除或把Controls历史流程依赖描述成已清零。
- 只清理本次触及的MainWindowViewModel模板化注释，写明默认路由、视图借用和关闭所有权；修正测试夹具旧StartupUri/单工具默认说明。没有全仓批量改名、整理或删除用户已有文件。

**实际门禁**：新增P6 8项；最终逻辑390/390、Vision WPF381/381、通用FlowEditor414/414，0失败/0跳过；Workbench显式增量构建0警告/0错误。日志vision-p6-{logic-full,wpf-full,floweditor-full,host-final}.log；最终另有vision-p6-focused-final.log 8/8（仅消除新增测试的可空静态分析警告，生产代码未再变）。截图与.flow/.vtemplate/输入输出PNG在Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p6-acceptance/。未做MachineApp或全仓构建、未运行生产设备。

**终验结论**：下面十项矩阵是本轮审计结果，测试总数不是全部需求通过证明。P3测量跟随/任意帧局部编辑、形状与AI授权验收、物理交互仍阻塞完整交付；受控兼容债务保留，去重/外部模型打包等明确后续。

**工作**：

- Workbench以通用FlowEditor承载视觉流程为主入口，复用Vision.Controls编辑节点；单工具入口按兼容需要保留，不维持另一套任务执行/保存逻辑。
- 默认布局：左侧任务实例/工具箱，中间操作栏与自定义图像，右侧参数/ROI/结果及能力附加页，底部执行信息。自由连线仅在已具备真实绑定能力且有需要时接入。
- 依据真实消费者删除本次改造造成的冗余，核查 C#/XAML、Prism、反射、资源URI和持久化标识；不凭静态零引用删除兼容入口。
- 随实际修改写清坐标、单位、所有权、失败和取消；清除相关模板化废注释，不做无关全仓重写。
- 用同一端到端场景复核十项：创建任务、导入图像、训练/加载、绑定定位、执行、查看结果、保存、关闭、重开、再次执行。

**验收**：

- 逻辑、WPF/STA、历史配置、对应宿主构建与人工操作证据分开报告。
- 真实SDK/设备未授权或不具备环境时，相关项列为阻塞/待验收，不把fake和跳过当成验收通过。
- 十项矩阵逐项列证据、残留差距及结论；不能以总测试数替代需求验收。
- 无尚未处理的生命周期阻断问题，无本批引入的无用入口/参数副本/执行器。

**执行后对比**：针对完整操作链重新联网对照至少两个优秀作品，而非只比较单个控件。更新计划版本，将剩余项分类为本期阻断、明确后续或不适用；只有十项矩阵达标后才报告本计划完成。

### P7：测量工具刚体定位跟随（建议范围，待确认）

状态：**尚未实施**。此前计划只定义P0–P6；本轮用户提出“执行P7”，但没有既定P7范围。P6定向8/8复核通过（%TEMP%/vision-p6-review.log），仅更新建议，不自行新增功能或销去前期残项。

**建议优先级**：先处理第8项的可离线功能阻断，而不是增加新页面或继续只做入口收口。

**建议首批范围**：
- OpenCV FindLine/FindCircle接入现有主任务定位来源、匹配索引及基准，复用原测量参数/几何和Card准备链；目标执行快照与原DTO分离，重复运行不累积变换。
- 保留原结果坐标/单位、失败/过期来源隔离、忙态和关闭规则，补实际测量几何/结果及保存重开测试。
- 限同输入、单位尺度平移/旋转；不增加另一套参数、引擎或隐式坐标推断。

**不自动纳入**：任意帧局部坐标编辑、跨裁剪坐标链、HALCON测量适配及授权运行、缩放/镜像/世界单位换算。它们继续列为未完成；是否合入P7或另拆子阶段须用户确认，不擅自缩减原十项要求。

**确认后门禁**：先复核原几何及能力声明，再实施/验证/执行后两来源对照，最后更新能力矩阵和第8项结论。当前没有P7实现、测试通过或完成结论。

## 4. 十项要求终验矩阵（P6本轮审计）

“离线范围通过”不等于物理UI或授权SDK验收。整体结论：**尚未全部达标**。

| 用户要求/阶段 | 实际证据 | 本轮结论 | 剩余差距 |
|---|---|---|---|
| 1 自定义显示 P4/P6 | VisionImagePanelTests、VisionViewportTests、VisionFlowP4Tests及P6原像素文件往返；主链无厂商显示窗口 | 离线范围通过 | 物理鼠标/跨屏待验；只支持明确像素格式；没有宣称所有算法支持16位/彩色 |
| 2 操作栏/四类图 P2/P4/P6 | 四视图真实取样、局部坐标隔离；P6实际任务加载/保存命令及重开；原像素与截图分开 | 离线范围通过 | 系统文件对话框、实图操作待验；当前左侧为任务画布，工具目录仍顶部下拉，不冒称完整VisionPro工具箱布局 |
| 3 底部执行信息 P0/P4/P6 | BrowserActionLayoutTests、P0/P1停止/关闭、P6执行与结果断言；实例/状态/耗时/图像记录分开 | 离线范围通过 | 同步厂商调用不承诺即时取消；真实SDK长调用与人工提示待验 |
| 4 属性特性/三基础页 P1/P4 | EditorRefinementTests、P4ToolEditorTests、参数/ROI/结果及能力模板页，原DTO和元数据编辑链保持 | 离线范围通过 | 原生模型训练/复杂实图ROI人工交互待验；不把所有复杂编辑硬塞PropertyGrid |
| 5 多工具Workbench P1/P2/P6 | 默认真实VisionFlowView；P2数据绑定与恢复；P6实际保存/宿主关闭/新会话重开/再执行 | 离线范围通过 | 不声称任意图/所有工具组合与全部物理连线已验；外部资源路径迁移待后续 |
| 6 简化/架构 各阶段/P6 | 单一原引擎/store/DTO；ControlsDependencyTests精确兼容守卫；源码消费者核查 | 受控兼容下部分达成 | 三个旧公开类型及Controls历史流程依赖保留，不能称纯Controls或纯Core节点；仓外消费者未知，不进行破坏性删除 |
| 7 多后端/AI/第三方 P1/P4/P5 | 19项目录及三家Provider保留；P5四AI身份/恢复/释放fake；第三方IOutputImageResult显示契约 | 契约通过，真实运行待证 | HALCON/AI Runtime/License/模型与预处理待验；异构第三方几何仍须呈现适配，不提供万能插件自动绘制 |
| 8 仿射/定位 P2/P3/P5 | P3真实模板→Blob平移、受控旋转、候选/同图像/旧结果隔离；P5实际第二候选；P6关闭重开无漂移 | **未全部完成** | 测量工具主链跟随、任意帧局部编辑、跨裁剪坐标链未实现；缩放/镜像/一般仿射不支持，不能近似抵扣 |
| 9 形状/灰度匹配 P3/P4/P5 | OpenCV真实文件/双候选/下游闭环；HALCON托管契约、RequiresRuntime用例保留 | 灰度离线通过，形状真实验收阻塞 | HALCON训练/.vshape/.shm/独立定位评分实测未执行；两类实图精度待验；OpenCV无旋转/尺度估计 |
| 10 注释 各阶段/P6 | 关键坐标、单位、所有权、失败/取消与兼容说明；本次MainWindowViewModel/夹具注释收口 | 本次触及范围达成 | 历史模板化注释仍存在，未作无关全仓重写，不能宣称全仓注释治理完成 |

### 残项分类与下一步

- **本期功能阻断**：P3测量跟随、任意帧局部坐标编辑/跨图坐标链。继续复用原几何/会话；未实现前第8项不勾完成。
- **授权/验收阻断**：HALCON形状真实训练/文件/匹配、三家AI真实模型/预处理/推理与资源回收、两后端实图精度。需用户在受控授权环境验收；不安装Runtime、去Skip或调用生产代验。
- **人工待验**：物理鼠标拖线/ROI、系统文件对话框及多显示器/DPI切换；模拟STA与截图不抵扣。
- **受控兼容保留**：三个旧公开类型、旧路由/资源URI、.visionproj v1/v2。当前默认任务不使用它们作第二主链；移除需独立兼容决策，不凭静态零引用。
- **明确后续**：P2资产去重、大图体积优化、外部模型打包/相对路径迁移、历史非关键注释治理；当前功能如实保留成本/路径提示。
- **当前不支持/不适用**：缩放、镜像、非均匀仿射和世界单位自动换算没有算法承诺；标定/取图工具不强塞搜索ROI跟随。任意Docking/端口预览不是本轮范围。

## 5. 执行后对照与计划修订模板

每阶段完成时在本计划的修订记录中填一行；证据细节放既有验证记录。

| 项目 | 必填内容 |
|---|---|
| 阶段与实际范围 | 做了什么、改动模块、明确没做什么 |
| 本地证据 | 命令与结果、回归案例、截图/人工验收、授权边界 |
| 联网来源 | 作品、官方URL、版本/分支、实际访问日期；失败访问如实记录 |
| 实现差距 | 本实现行为 → 对方行为 → 用户影响，不只写“界面更好看” |
| 取舍 | 采纳/暂缓/不采纳，理由与范围影响 |
| 计划更新 | 下阶段动作、验收标准、阻塞、状态和新版本号 |

修订不得覆盖旧事实或把未执行项勾为完成。修复代码后先复测，再更新“已完成”状态。

### P0执行后联网对照（2026-10-24）

本地证据：`vision-p0-before.log`为原5项失败；`vision-p0-provider-before.log`为Provider遗漏的1项失败；`vision-p0-focused.log`定向38项通过；`vision-p0-logic-full.log`逻辑380、`vision-p0-wpf-full.log`WPF310项通过；`vision-p0-workbench-build.log`构建0错误。日志均在本机TEMP，详细命令记于既有验证记录。

| 作品与实际来源 | 对方可核实行为 | 本实现实际证据与差距 | 取舍及计划更新 |
|---|---|---|---|
| [Aurora Preview and Data Presenting](https://docs.adaptive-vision.com/5.5/studio/getting_started/PreviewAndDataPresenting.html)，文档5.5.6，本次HTTP200 | Move/Zoom属于预览导航，不修改程序图像；保存原图和保存视图区分 | STA打开真实任务页，在运行中切到未执行实例，确认参数/ROI/重复执行禁用、画布仍启用；点击真实放大菜单后，渲染白色图像宽度增加，平移命令可切换。未做物理拖拽或竞品软件实机测试 | 采纳显示与编辑分离，作为P1迁移/P4的持续回归；多窗口/Docking不采纳为本期新增范围 |
| [OpenPnP CvPipeline.java](https://github.com/openpnp/openpnp/blob/develop/src/main/java/org/openpnp/vision/pipeline/CvPipeline.java)，develop，本次HTTP200 | pipeline保存阶段结果，明确克隆图像的归属，在release/close释放自身保留的原生资源 | 本实现继续由卡片工作区拥有工具/结果/Provider；关闭同时等待流程节点和卡片实际任务，聚合清理错误。fake证明一个失败不跳过其它实例或Provider，迟到结果恰好清理一次；尚非真实SDK验收 | 采纳明确资源拥有者；不照搬Java finalize、图像克隆或线程策略。P1迁移前必须提供所有权表并保留P0回归 |

源码与文档快照仅保存在本机 `%TEMP%/vision-p0-comparison/`；访问时间为2026-10-24 04:51 +08:00。Aurora提取文本SHA256为499EC2772B85FB687E20B251AC93B2E011B43F422D61A51A55F73930E1E85B64，OpenPnP源码快照SHA256为5559D53EADA7D76FD8AAC953EC4269DEBCA7A0B40F2CA01ACBF927B26773E610。快照用于溯源，不代表复刻或完整功能对等。

### P0宿主关闭收尾对照（2026-10-24）

本轮仅在既有VisionTaskP0Tests增加2项集成用例：整任务执行与当前工具命令执行分别经过本地测试窗口的真实Closing事件、WorkbenchLifetime和CloseRegionAsync。先放入一个释放失败的非活动缓存页，再清理活动页；窗口必须保持到实际后台调用完成才关闭，工具/结果释放一次，缓存页错误仍可观察。仅关闭测试窗口，未关闭共享测试主窗、真实设备或用户进程。

定向2项通过（vision-p0-host-close-first.log），WPF全量312项通过（vision-p0-host-wpf-full.log）；没有新增产品修复，也不把这2项称为先红后绿。P0现在有6项红绿回归和2项宿主集成用例。

执行后于2026-10-24 05:02 +08:00再次联网读取上表Aurora与OpenPnP来源，均HTTP200，快照SHA256与V1.2记录相同，保存于 `%TEMP%/vision-p0-host-comparison/`。Aurora描述创建/关闭的是预览，OpenPnP的release/close由pipeline回收其拥有资源：继续采纳预览生命周期与算法资源归属分离。它们没有证明WPF异步关闭正确；本项目该项依据是新增Closing集成用例。P1迁移门禁补入此宿主链回归，不新增Docking、错误对话框策略或视觉流程引擎。

### P1执行后联网对照（2026-10-24）

本地证据：vision-p1-p0-recheck.log为P0 8项复核；vision-p1-focused-gate-final.log定向24通过；vision-p1-logic-final.log逻辑380、vision-p1-wpf-final.log WPF319通过；vision-p1-floweditor-regression.log通用FlowEditor相关24项通过；vision-p1-workbench-final.log增量构建0警告/0错误。截图补验vision-p1-visual-close.log两项通过，排空通用控件Loaded绘制队列后实际连线层非空。前置取消问题的失败证据为vision-p1-cancel-before.log（期望null，实际旧Result）。日志均在本机TEMP。

| 作品/来源 | 实际核实内容 | 本实现证据与差距 | 取舍及后续 |
|---|---|---|---|
| [MERLIC Tools](https://www.mvtec.com/doc/merlic/5.6/manual/en-us/Content/Creator/Merlic_concepts/Tools/tools.html)，5.6.2，HTTP200 | Tool Flow展示工具/工具组、连接及错误/警告状态；MVApp参数/结果另有语义，不是画一条线就完成绑定 | STA已加载真实通用画布、两个实例、Next连接并由原引擎执行；切换选中实例保持原DTO/ROI/结果，失败停止下游。当前尚无视觉来源绑定/任务恢复，也未物理拖线 | 采纳实例/状态/控制连接可见；显式数据来源及MVApp式任务接口留P2/P3，不将Next当数据绑定或扩展成完整MERLIC功能 |
| [OpenPnP CvPipelineEditor.java](https://github.com/openpnp/openpnp/blob/develop/src/main/java/org/openpnp/vision/pipeline/ui/CvPipelineEditor.java)，develop，HTTP200 | 编辑器构造接收既有CvPipeline并保存引用，组合PipelinePanel与ResultsPanel，不为每次编辑重建另一份pipeline | 新节点接管Core会话；共享编辑视图借用同一Card/DTO/结果，不再包装BrowserWorkspace；两个执行入口经过实际Closing链回收。旧公开类型仍保留，Controls历史流程依赖未清零 | 采纳借用原实例及编辑/执行所有权分离；不照搬Java/Swing、反射注册表或线程策略；P1继续处理兼容依赖与退役资源，不能仅以新增项目宣称完成 |

联网快照在本机 %TEMP%/vision-p1-comparison/：MERLIC提取文本SHA256 6B3F62D0B3332255B8FCC44CB2E3C393DED7A1ED8DF04276B881DA645E110A74；OpenPnP源码SHA256 F126E874FBF7FCD24C16A1AC554AA7C6B69D17452D869F5FF1FE24E905AE8053。完整门禁后07:36 +08:00重新访问均HTTP200；后续仅补截图队列/可见连线断言且两项通过，无生产代码改变。没有竞品软件实机运行证据，Cognex仍未作为核实依据。

### P1复核收口后的对照（2026-10-24 07:51 +08:00）

逻辑381/WPF322通过后，再次访问MERLIC 5.6 Tools和OpenPnP develop CvPipeline源码，均HTTP200。MERLIC仍区分工具连接、状态与任务参数；OpenPnP release/close归还pipeline保留资源。本轮据此采纳实例移除后的显式退役和错误观察，不借鉴Java finalize或自动销毁借用视图。实际验证包含移除空闲节点、移除正在调用的节点、清理失败后的错误保留；未做授权SDK验证。快照在%TEMP%/vision-p1-close-comparison/，MERLIC原始HTML SHA256 110B29A946F4687D99F513A07A6F2F96BA9D7A6311CF258AAFD866D889434CC6，OpenPnP源码5559D53EADA7D76FD8AAC953EC4269DEBCA7A0B40F2CA01ACBF927B26773E610。原图/ROI/模型、参数DTO及旧类型身份保持；受控兼容引用移交P6并非宣称已消失。

### P2执行后联网对照（2026-10-24 08:32 +08:00）

在最终39/381/334及宿主构建结果之后重新访问两个来源，均HTTP200。截图使用本机Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p2-acceptance/crop-blob-bound.png，展示真实通用控制线/图像数据线、绑定选择与本次结果；不是竞品软件或物理交互验收。

| 来源 | 核实事实 | 本实现达到/差距 | 取舍 |
|---|---|---|---|
| MERLIC 5.6.2 [Tools](https://www.mvtec.com/doc/merlic/5.6/manual/en-us/Content/Creator/Merlic_concepts/Tools/tools.html) | 工具流展示实例/连接/状态，MVApp参数与结果另有独立语义 | 本次绑定不再只画Next：真实Crop输出改变Blob输入像素，重开与手动运行一致。尚无完整MVApp式任务接口、定位来源界面或资产去重 | 采纳显式数据来源与控制流区分；定位来源留P3，额外任务接口暂缓，不扩为完整MERLIC复刻 |
| OpenPnP develop [CvPipeline.java](https://github.com/openpnp/openpnp/blob/develop/src/main/java/org/openpnp/vision/pipeline/CvPipeline.java) | 声明toXmlString/fromXmlString可交换pipeline；getResult/阶段名称取得阶段结果，缺少阶段或结果明确报错；pipeline拥有保留资源 | 使用原.flow及严格配置扩展，重开无运行结果/SDK加载；按连接取得本次图像，来源缺失不回退。通用全量尚有3目录治理失败，大图/零节点输入状态仍待收口 | 采纳可恢复配置与实际阶段输出分离、缺失结果报错；不照搬XML/Java生命周期，不增加平行文档引擎或克隆所有运行结果 |

快照仅在%TEMP%/vision-p2-comparison/。MERLIC原始HTML SHA256为110B29A946F4687D99F513A07A6F2F96BA9D7A6311CF258AAFD866D889434CC6，OpenPnP源码为5559D53EADA7D76FD8AAC953EC4269DEBCA7A0B40F2CA01ACBF927B26773E610。P2的ROI共享、旧单工具误导入及执行输入尺寸残留均有先红后绿证据，详细命令/失败记录见EDITOR_REFINEMENT。

### P2复核与P3执行后对照（2026-10-24）

P2本轮先修复/核实治理入口与零节点输入：历史中心报告缺失不予复造，新增真实模块架构说明并保持所有SolutionItems/项目路径的存在断言。空任务输入回归先失败后通过；逻辑381/WPF336、通用414/414通过。08:55 +08:00再次读取MERLIC Tools/OpenPnP CvPipeline，HTTP200，快照%TEMP%/vision-p2-close-reference/，哈希与V1.7原始资料相同。按显式文档/结果归属原则进入P3，不建立视觉文件引擎。

P3最终逻辑385/WPF349通过、宿主构建0错后，10:02 +08:00重新联网对照：

| 来源 | 核实行为 | 本实现达到/差距 | 采纳与后续 |
|---|---|---|---|
| [Aurora Using Local Coordinate Systems](https://docs.adaptive-vision.com/5.5/studio/machine_vision_guide/LocalCoordinateSystems.html)，5.5.6，HTTP200 | 模板输出alignment，高层工具接inAlignment；输入可用局部坐标，输出/叠加必须转回绝对图像坐标；不同坐标系不可混用；局部编辑可自动对齐 | 主界面定位来源/候选/基准到Blob已连通，实际派生ROI和结果在输入坐标显示，有同图像身份与失效检查。尚无自动对齐的任意帧局部编辑，测量工具适配未完成 | 采纳来源显式连接、原始/派生几何分离与同坐标叠加；局部编辑/测量继续留P3，不把刚体实现描述为含Scale的完整CoordinateSystem2D |
| [OpenPnP AffineWarp.java](https://github.com/openpnp/openpnp/blob/develop/src/main/java/org/openpnp/vision/pipeline/stages/AffineWarp.java)，develop，HTTP200 | 明确支持scale/shear/mirror；坐标为实际长度单位、相对相机中心且Y向上；后续阶段须理解像素/单位尺度变化 | 当前是图像像素、Y向下的Pose2D刚体，对非单位尺度明确拒绝，不做真实长度/相机中心换算。无剪切/镜像能力 | 不照搬单位、符号或算子实现；采纳能力/单位必须明确的边界，缩放/镜像暂缓，不能拿对方功能给本项目抵扣 |

P3快照在%TEMP%/vision-p3-reference/，Aurora文本SHA256 0AE6ECB26E95706BAFD22EF20AD4E57810A6731857FECDA35FD9C9E5ED3B3460；OpenPnP源码75487B983539805374C8CA9550A4CDC3A4716DF4AA2FAD957AB4970B80AA4547。MERLIC Working_with_ROIs尝试返回404，列待证，不作为已核实依据。源码/文档与本地96-DPI合成截图对照，不是竞品软件实机或授权SDK验收。

### P3复核/P4执行后对照（2026-10-24 10:44 +08:00）

在最终386/358与宿主0警告/0错误之后再次取得以下官方资料，均HTTP200；快照%TEMP%/vision-p4-reference/release/（10:27第一次读取亦保留）。这是文档/源码与本地STA截图对照，不是竞品实机验收。

| 来源 | 核实事实 | 本实现达到与差距 | 取舍 |
|---|---|---|---|
| [Aurora Preview and Data Presenting](https://docs.adaptive-vision.com/current/studio/getting_started/PreviewAndDataPresenting.html)，current | Image Tools只改变预览不修改程序图像；Save Image As保留原尺寸，Save View As另有视口/标注语义；图像与几何可在同一预览显示 | 四视图/缩放不改输入或模型；PNG/TIFF导出原像素；局部训练图不混画输入ROI/结果。已有结果互选、单行状态、模拟DPI/重载有回归。尚无任意端口预览、Docking及显示颜色/归一化工具；未提供产品截图命令 | 采纳显示与算法状态分离、数据来源/尺寸明确；截图与原像素绝不混称。额外窗口和显示算子暂缓，不以竞品范围扩张需求 |
| [OpenPnP CvPipelineEditor.java](https://github.com/openpnp/openpnp/blob/develop/src/main/java/org/openpnp/vision/pipeline/ui/CvPipelineEditor.java)，develop | 构造保留传入pipeline，组合PipelinePanel/ResultsPanel，结果刷新与所选stage关联 | 共享编辑器继续借用原Card/模板/结果，后端绘制适配移到Presentation而非画布；卸载重载不重建工具，缓存重用断言保留。无通用第三方几何插件注册器，不照搬Java/Swing或任意反射编辑机制 | 采纳原实例与呈现分离；沿用现有元数据/能力入口，不增加第二调度器、DTO或UI工厂框架 |

Aurora原始HTML UTF-8快照SHA256：9F8E4D7F0C8BB6E2A003D514758120FFB66BFEFDC7EB43BE35F0F9423D616AE5；OpenPnP源码SHA256：18FB94F04AA89238B4398F91FFF1A860F260F0BB30791D986BCA6F8EBCDB6462。Cognex本轮未重试/核实，不作来源；未把Aurora的深度学习训练编辑器视为本项目已支持AI训练。

### P4复核/P5执行后对照（2026-10-24 11:08 +08:00）

最终390/373及宿主显式重跑0警告/0错误之后，再次访问两个官方来源，均HTTP200；快照%TEMP%/vision-p5-reference/。没有运行竞品或授权SDK，也没有声称Cognex本轮已核实。

| 来源 | 核实事实 | 本实现达到/差距 | 采纳或暂缓 |
|---|---|---|---|
| [Aurora Creating Models for Template Matching](https://docs.adaptive-vision.com/current/studio/user_interface/CreatingModelsForTemplateMatching.html)，current | 模板图/区域、模型参数和运行匹配参数分开；模型准备好后连接匹配滤波器；其模型编辑支持角度、尺度与相应精度/性能取舍 | 本实现保留原取样、训练、运行参数、文件与原结果，新增状态/引用一致性及实际模型→双候选→定位消费。OpenCV仍固定方向/尺寸，未实现Aurora式灰度旋转/缩放模型或任意形状模板编辑 | 采纳准备/有效性/运行分离和真实候选；额外算法需独立精度/速度目标，本轮不加伪参数，不称功能等价 |
| [HALCON 23.11 find_scaled_shape_model](https://www.mvtec.com/doc/halcon/2311/en/find_scaled_shape_model.html) | 搜索参数有角度、尺度、最小分数、候选数量、重叠等；参考位置受模型原点/创建域重心语义影响，图像边界/金字塔会影响检出 | 原ShapeMatch工具保留相应参数与真实返回值、模型包与旧shm能力；定位消费仍拒绝非单位Scale。授权环境未跑训练/文件/匹配，不能拿算子文档或托管结果抵扣 | 采纳保留真实姿态/尺度/参考点、训练/搜索范围清楚分离；不把Center字段臆造为物体外接框中心，不承诺跨后端相同评分/精度；真实验收保持待证 |

Aurora原HTML UTF-8快照SHA256：431AAFA4941FD14E4E514F04647EDDF8795E0BDF326C4F39279532F18342FC70；HALCON官方HTML快照SHA256：384B12E101015334589AC0B9F30423A56953B389E781532FA448B1A82EE1CB8A。P5截图来自本项目实际OpenCV闭环，文件p5-acceptance/model-fixture-blob.png，不是竞品画面或物理鼠标验收。

### P5复核/P6执行后对照（2026-10-24 11:33 +08:00）

在390/381/414、宿主0警告/0错误及最终P6 8项之后重新取得以下来源，均HTTP200；快照%TEMP%/vision-p6-reference/final/。11:31初次快照亦保留。对比的是完整文件→工具配置→连接/执行→结果/保存重开职责，不是竞品实机操作。

| 来源 | 核实事实 | 实际对照与差距 | 取舍 |
|---|---|---|---|
| [Aurora First Program](https://docs.adaptive-vision.com/current/studio/getting_started/FirstProgram.html)，current | 从文件加载图像、选工具、配置/连接，区域分割/Blob分析、表格索引与图像几何关联；预览来自端口拖入 | 本轮实际文件/取样/模型/显式定位/Blob/保存关闭重开闭环，已有结果身份互选；没有任意端口预览、完整左工具箱或实际物理拖线证据 | 采纳实例、真实数据与结果身份闭环；继续用原FlowEditor，不照搬Aurora全功能或把Next当图像绑定 |
| [OpenPnP CvPipeline.java](https://github.com/openpnp/openpnp/blob/develop/src/main/java/org/openpnp/vision/pipeline/CvPipeline.java)，develop | 提供toXmlString/fromXmlString、按阶段取结果，release/close清理由pipeline拥有的资源 | 原.flow与严格配置恢复、不恢复结果/句柄；实际宿主关闭后新会话重开再执行。仍有外部模型路径与兼容债务，未打包全部资产 | 采纳配置/运行结果分离和所有者负责关闭；不另建XML/Java引擎，不以测试关闭证明SDK取消或物理资源验收 |

Aurora原始HTML UTF-8快照SHA256：AB56A00F82A37FE378A855571AF155DAD12B615161F9E347651285D916664934；OpenPnP源码：E0398B56036DC80394427A8B643580C584ACDFA1E1B421A53A596C5A125C147B（develop可变化，仅对应本次快照）。原像素、关窗及复开截图来自本项目离线合成场景；未重试Cognex、未宣称其已核实。

### 多工具组合与工具箱收敛（V1.17–V1.18，2026-10-24）

- V1.17组合模拟：真实OpenCV的Crop→Threshold→Morphology→Blob、六节点双支路、有效全黑输出、失败停止与独立支路、同裁剪图匹配→定位→Blob及.flow重开，共14项。按失败证据修复上游失效后的旧输入、同裁剪图设基准误拒、通用数据连线撤旧预览/来源诊断、Blob恢复后面板缓存默认值。未加入隐式跨裁剪坐标变换；不同坐标系仍拒绝。
- V1.18入口：顶部工具下拉/添加按钮移除，右侧直接复用FlowToolboxView、NodePaletteItem与分类模板。原19项目录只做元数据投影，不另注册工具、执行器或文件格式；双击与拖入统一调用原Add及会话所有权路径。小于1200 DIP自动收起工具箱，可手动展开。搜索仍可使用，执行/退役/关闭时不能添加。
- 独立“单工具调试”从主导航移除，原Prism路由/API/.visionproj兼容保留；节点内执行当前工具及自动化单工具回归继续保留，不把UI收敛等同删除测试。
- 本轮新增2项工具箱回归，P6端到端改从真实工具箱绑定命令添加节点，其余模型/像素/定位/文件/关闭断言保留。定向24、WPF397、逻辑390、通用FlowEditor414通过，0失败/0跳过；最终宿主增量构建0警告/0错误。通用首轮413/414的新增私有方法注释守卫失败已补真实说明后重跑414；未放宽断言。
- 实际截图检查：processing-branches.png、crop-match-blob.png；位于Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/combination-acceptance。宽窗口右侧目录与图像/参数并列，900 DIP收起有STA布局断言。绑定命令/创建入口验证不代表物理双击或OLE拖放；原生鼠标、物理DPI跨屏、授权SDK/真实模型继续待验收。

执行后2026-10-24 16:40 +08:00重新访问以下两来源，均HTTP200，快照%TEMP%/vision-toolbox-reference/：

| 来源 | 已核实内容 | 本轮取舍与差距 |
|---|---|---|
| [Aurora First Program](https://docs.adaptive-vision.com/current/studio/getting_started/FirstProgram.html) | 从Toolbox/Filter Catalog选工具、连接和配置；结果预览来自端口 | 采纳“一个任务内选工具、配置、组合执行”的主路径，工具箱直接用既有FlowEditor控件；不照搬布局方位，不声称实现任意端口预览 |
| [OpenPnP CvPipelineEditor.java](https://github.com/openpnp/openpnp/blob/develop/src/main/java/org/openpnp/vision/pipeline/ui/CvPipelineEditor.java) | 编辑器支持添加/移除阶段、修改阶段属性、查看阶段图像/模型结果，允许注册扩展stage | 继续使用原目录/实例与所选节点编辑器，不新建平行注册表；没有新增第三方几何绘制插件框架，不把源码核查当竞品实机验收 |

Aurora SHA256：AB56A00F82A37FE378A855571AF155DAD12B615161F9E347651285D916664934；OpenPnP SHA256：18FB94F04AA89238B4398F91FFF1A860F260F0BB30791D986BCA6F8EBCDB6462。V1.17组合执行后12:15另核实RunningAndAnalysingPrograms与CvPipeline，记录见EDITOR_REFINEMENT。P7尚未实施，测量跟随、任意帧局部编辑、资产去重/模型迁移及授权/物理验收不自动销项。

### 修订记录

| 版本 | 触发 | 实际变更 | 后续动作 |
|---|---|---|---|
| V1.0 | 用户要求制定并在执行后联网对比、更新计划 | 建立P0–P6、阶段门禁、十项映射及强制对照闭环；制定时未执行产品修改 | 从P0已知问题的失败回归开始 |
| V1.1 | 用户确认Workbench直接复用FlowEditor、增加Vision.FlowNode且保留全部后端 | 明确目标依赖与迁移边界，重订P1/P2/P3/P6；不增加Vision.FlowEditor。P0已有5项失败回归，部分修复未验证 | 继续完成P0；验证及执行后联网对照完成后更新为V1.2，不能提前标记通过 |
| V1.2 | P0修复、完整离线验证和执行后联网对照 | 修复三项问题及Provider遗漏；6项红绿回归、逻辑380/WPF310通过、宿主构建0错；核实Aurora与OpenPnP，补充P1所有权/迁移回归和P4预览边界 | P1待执行；保留真实鼠标/显示器与授权SDK验收，不自动增加Docking或竞品全功能 |
| V1.3 | 用户要求继续P0，补查宿主集成 | 仅补2项真实Closing/Region缓存页用例，定向2/WPF312通过；再次核实两个公开来源，生产代码未改 | P0离线收尾完成；P1迁移须保留宿主关闭链回归，人工/授权SDK验收仍单列 |
| V1.4 | 用户要求复核P0并执行P1 | P0 8项复核通过，先确定所有权表、扩展入口和兼容限制，授权P1实施中 | 以真实主路径及生命周期回归为准，不将结构目标冒充完成 |
| V1.5 | P1主路径实施、离线验证与联网对照 | 新增Vision.FlowNode及轻量会话；Workbench直接使用通用画布；24/380/319及通用24通过，构建0错；前置取消有独立红绿证据 | P1兼容依赖和移出画布实例退役策略仍待收口，未进入P2 |
| V1.6 | 复核P1、更新计划并执行P2 | 移除实例即时/延迟退役有红绿与运行回归；381/322通过；旧三个公开类型精确守卫及兼容保留策略核实，执行后两来源重核 | P1主路径门禁通过，兼容债务交P6；P2实施中，不能把目标配置恢复当已完成 |
| V1.7 | P2核心闭环实施、完整验证及联网对照 | .flow配置恢复/候选保旧/原子替换、显式真实图像绑定、复制ROI独立；39/381/334通过，宿主0错；通用414项中411通过/3目录资料失败 | 不进入P3；先解决完整模块门禁和图像资产/零节点状态收口，兼容类型债务仍交P6 |
| V1.8 | 用户复核P2并授权P3 | 当前真实治理入口、零节点输入与大图往返补验，414/381/336通过并执行后重核来源 | P2复核通过，开始P3；图像去重限制作明示后续 |
| V1.9 | P3主闭环实现与完整Vision离线验证、执行后对照 | 显式定位候选/基准/来源、同帧校验/失效传播、真实Blob跟随和派生叠加；13项P3场景、385/349通过，构建0错；正交像素及迟到叠加有红绿证据 | P3仍有测量工具/局部编辑/授权验收边界，不进入P4；不宣称缩放/镜像或全部工具完成 |
| V1.10 | 用户要求复核P3并执行P4 | P3定向13/13通过，保留测量/局部编辑/授权边界；授权推进独立显示子项，训练图及输出契约开始实施 | 不伪造P3整体完成；P4代码与证据、执行后对照待验证 |
| V1.11 | P4独立显示/呈现实施、完整Vision门禁与执行后对照 | 四类真实图像记录/训练局部坐标隔离/通知所有权、输出契约及绘制呈现边界；新增9项P4和1项守卫，386/358通过，宿主0警告/0错误 | 保留P3未完成项与P4物理/授权验收；不自动进入P5，不增加Docking/AI训练 |
| V1.12 | 用户复核P4并授权P5 | P4四视图/缓存定向12/12通过；复用原模板/模型文件及AI会话实施P5，锁定不扩张灰度算法/SDK边界 | 修复引用与命令守卫，补文件/任务/定位和四AI身份失效证据 |
| V1.13 | P5离线实现、完整Vision门禁和执行后两来源对照 | 15项P5与4项设备契约通过，390/373及宿主0警告/0错误；模型引用/只读模态守卫/设备拒绝有红绿证据；超时与独立重跑分开保留 | P5授权SDK/实图、P3残项及P6终验仍未完成，不宣称全部十项完成 |
| V1.14 | 用户复核P5并授权P6 | P5定向15项复核通过；锁定默认任务入口、保存关闭重开及十项矩阵，不销P3/授权残项 | 基于源码核查兼容消费者，不凭陈旧图删类型；补原像素导入闭环 |
| V1.15 | P6离线实现、全模块门禁与执行后对照 | 默认任务入口/原保存命令，8项P6、390/381/414与宿主0警告/0错误；文件导入降位/转灰有红绿证据，十项矩阵逐项分类 | P6整体终验与整个计划仍未完成；按本期功能、授权/人工、兼容保留及明确后续处理，不以总测试数勾全完成 |
| V1.16 | 用户要求复核P6、更新计划并执行P7 | 源码抽查默认导航/文件导入与P6测试；定向8/8通过，无生产代码改动；原计划无P7，补建议范围并明确待确认 | 确认P7是否优先做OpenCV测量工具刚体跟随；未执行P7，不销P3/授权/人工残项 |
| V1.17 | 用户要求对标、模拟多工具组合并逐步优化 | 14项真实OpenCV组合，修复旧输入、裁剪基准、连线失效诊断、Blob面板恢复4类问题；390/395通过，宿主0警告/0错误，执行后两来源重核 | 保留跨裁剪坐标、测量、授权/物理验收边界 |
| V1.18 | 用户要求继续并提出右侧工具箱/收起单工具入口 | 复用FlowToolboxView及原Add，主导航仅任务；2项工具箱回归、P6从工具箱添加，24/390/397/414通过，最终宿主0警告/0错误，执行后两来源重核 | 原生鼠标拖放、DPI/真实SDK仍待验收；十项整体未全部达标 |
| V1.19 | 用户纠正工具箱应位于左侧 | 仅修改列顺序/展开方向，布局断言改验证工具箱→画布→编辑器；定向24项及宿主增量构建通过，执行后两来源重核 | 不重记历史全量为本轮通过，既有能力/物理验收边界不变 |
| V1.20 | 用户要求对比其它视觉产品并制定新增工具计划 | 核查19项/遗留5候选、三家官方文档，新增TOOL_EXPANSION_PLAN；规划16候选工具与E0–E4门禁，未新增运行代码或运行测试 | 先确认/实施最小基础批次，再逐阶段验收；不冒充工具已交付 |
| V1.21 | 用户授权开始执行新增计划 | 首批T01/T02/T05、目录22项，真实组合/会话关闭重开/ROI统计与失效清理；423/404通过，宿主增量构建0警告/0错误，执行后两来源重核 | 下一小批次T03/T04/T06；E0测量跟随/其余阶段与授权/物理验收不自动销项 |
| V1.22 | 用户要求继续工具扩充 | T03/T04/T06与两条新组合/重开/失效清理，目录25项；451/409通过、宿主增量构建0警告/0错误，执行后两产品与后端源码对照 | 先补E0数值/几何消费及测量跟随，再推进E2；ROI/Otsu/图形直方图与原验收残项不自动销项 |
| V1.23 | 用户要求先分离完整编辑器、补齐OpenCV＋HALCON，且节点属性必须直接用现成FlowEditor.Controls | 制定R0–R7完整计划、双后端矩阵、窗口/会话所有权与验收；核查HALCON桥/文件取图/原生直方图差异和四个官方来源，仅更新文档 | 下一次先实施R0，不造节点属性控件；当前仍25项/原内嵌布局，未执行测试或新SDK |

## 6. 初始公开参考（不是执行后的对比证据）

以下资料在前轮审查中已实际访问；每阶段完成后仍需按对比点重新核查，不能把本列表当成完成了执行后对比。

- Aurora：[Local Coordinate Systems](https://docs.adaptive-vision.com/5.5/studio/machine_vision_guide/LocalCoordinateSystems.html)
- Aurora：[Creating Models for Template Matching](https://docs.adaptive-vision.com/5.5/studio/user_interface/CreatingModelsForTemplateMatching.html)
- Aurora：[Preview and Data Presenting](https://docs.adaptive-vision.com/5.5/studio/getting_started/PreviewAndDataPresenting.html)
- MERLIC：[Tools](https://www.mvtec.com/doc/merlic/5.6/manual/en-us/Content/Creator/Merlic_concepts/Tools/tools.html)
- MERLIC：[Working with ROIs](https://www.mvtec.com/doc/merlic/5.6/manual/en-us/Content/Creator/Building_mvapps/Configuring_tools/working_with_rois.html)
- OpenPnP：[Pipeline源码](https://github.com/openpnp/openpnp/tree/develop/src/main/java/org/openpnp/vision/pipeline)
- Cognex VisionPro：前轮官方文档返回429，暂不作为已核实对比证据；执行阶段重试并记录实际可用的官方页面。
