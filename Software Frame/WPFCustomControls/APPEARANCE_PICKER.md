# 紧凑外观入口

位置：`WPFCustomControls/Controls/Views/AppearancePickerView.xaml`，CLR 命名空间仍为 `WPFCustomControls.Controls`。

```xml
xmlns:controls="clr-namespace:WPFCustomControls.Controls;assembly=WPFCustomControls"

<controls:AppearancePickerView Theme="{Binding Theme}" />
```

Theme 使用宿主现有的 Extension.UI.Theme.ThemeService。控件不替换自身 DataContext，不拥有或释放主题服务，不引用 Vision 等业务模块。未打开时只有76×30的图标/文字入口；点击后延迟创建并复用 ThemePickerView，保留现有主色、辅色和深浅选择方式，以及动态字段背景/前景。

弹窗收起或控件卸载时，内部主题选择 VM 退订借用服务的通知；重新打开时重新回灌并订阅。未传 Theme 的行为沿用 ThemePickerView 的兜底主题方式；正式宿主建议显式注入自己的服务。

Vision.Workbench 通过编辑器的 ToolbarAccessory 插槽放到工具条最右侧，没有另占一行，也不让 Vision.Controls 持有主题服务。主窗口观察区域内容更换，并在卸载时解除观察、清除自己提供的插槽对象，不释放编辑器工作空间。

本轮验证：Vision 的 AppearanceEntryTests 三项覆盖延迟创建、同一主题服务、关闭/卸载/替换服务退订、宿主插槽与900宽度不覆盖执行按钮；WPFCustomControls.Tests 120项通过。实际系统高DPI、真实鼠标弹层及其它宿主样式仍需人工验收。
