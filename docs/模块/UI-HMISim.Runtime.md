# 模块设计说明书：UI-HMISim.Runtime / HMISim.Controls

- **所属分层**：UI
- **对应程序集**：HMISim.Runtime（运行端）、HMISim.Controls（UI 组件库）
- **文档版本**：v0.2　**最后更新**：2026-11-17

## 1. 模块概述

阶段 0 最小运行端壳：把实时数据库的值呈现到画面上，并按质量码变色；附带写入与模拟拔线/恢复的演示操作。组态端、工程文件加载、画面设计器不在本阶段。

**依赖关系**：Runtime 依赖 Core/DriverSdk/Common/Drivers.Fins/Controls，演示期额外引用 Simulators.Fins（正式运行端剔除）；Controls 只依赖 Common。

## 2. 对外契约

| 契约 | 形式 | 说明 |
|---|---|---|
| `ValueDisplay` 控件 | WPF UserControl | 依赖属性三件：Title / DisplayValue / Quality；只认质量码、不认协议 |
| 常用控件集 | WPF UserControl ×6 | 数值显示/文本标签/按钮/指示灯/数值输入/进度条，经 ScreenPlayer 模板分发；交互控件写入经 ControlItemViewModel 的 writeSink 转交设备写队列 |
| 输入值转换 | Core 纯逻辑 | `PointValueParser.TryParse`（文本→按 TagDataType 转值，按钮与数值输入共用），可单测 |
| 质量呈现规则 | Common 纯逻辑 | `QualityPresentation`（文案+颜色）与 `ValueFormatter`（值格式化），可单测 |

## 3. 类清单

| 类名 | 所在文件 | 作用（一句话） | 备注 |
|---|---|---|---|
| ValueDisplay | Controls/ValueDisplay.xaml(.cs) | 数值显示卡片：标题+值+质量圆点/文案，Bad 时值变红 | 组件库种子控件 |
| ActionButton | Controls/ActionButton.xaml(.cs) | 按钮：点动走按下/松开两段写、其余模式 Click 一次写；Bad 时边框红 | 写入分发在 ControlItemViewModel |
| IndicatorLamp | Controls/IndicatorLamp.xaml(.cs) | 指示灯：亮绿/灭深灰，Bad 转灰描红边（坏数据不冒充关） | — |
| ValueInput | Controls/ValueInput.xaml(.cs) | 数值输入：当前值只读显示 + 输入框写入，错误回显 InputError | 输入中途不被刷新覆盖 |
| LevelBar | Controls/LevelBar.xaml(.cs) | 进度条：Fraction×轨道宽，填充色随质量 | 比例由 VM 按量程换算 |
| ScreenPlayer / ControlItemViewModel | Controls/ScreenPlayer.xaml(.cs)、ViewModels/ | 画面播放器与控件项 VM（快照刷新 + 按钮/输入写入意图） | 组态预览与运行端共用 |
| MainViewModel | Runtime/ViewModels/MainViewModel.cs | 装配运行时、200ms 节拍批量刷新、写/拔线/恢复命令 | 演示点位写死，与 DemoHost 一致 |
| PointRowViewModel | Runtime/ViewModels/PointRowViewModel.cs | 一行点位的显示状态 | 由快照 Apply 刷新 |
| ViewModelBase / RelayCommand | Runtime/ViewModels/ViewModelBase.cs | 零依赖 MVVM 样板 | 后续可换 CommunityToolkit，先不引包 |

## 4. 关键类详述

### 4.1 MainViewModel

**作用**：窗口的全部业务逻辑：启动进程内模拟器与 RuntimeHost、定时器批量把快照刷到行 ViewModel、状态栏汇总设备帧计数。

**方法清单**：

| 方法 | 作用 | 参数要点 | 返回值 / 质量约定 | 线程要求 |
|---|---|---|---|---|
| Start | 启模拟器+运行时+刷新定时器 | 窗口 Loaded 时调用 | — | UI 线程 |
| StopAsync | 停机并释放 | 窗口 Closed 时调用 | — | UI 线程 |
| Refresh（私有） | 200ms 节拍批量刷新 | 遍历 Rows 读快照 | 质量经 ValueDisplay 变色呈现 | DispatcherTimer，UI 线程 |

## 5. 约定与注意事项

- 刷新走「拉」模型（定时器批量读快照），不订阅逐变量事件——画面点位再多也不会出现 Dispatcher 抖动；后续画面系统沿用此节拍刷新机制。
- 全局样式只放外观（本阶段 App.xaml 未设任何全局行为属性，遵守规范第 9 节反例）。
- 编译可在非 Windows 环境以 EnableWindowsTargeting 交叉验证（已实测），运行必须在 Windows。

## 6. 参考来源

无。

## 7. 变更记录

| 日期 | 版本 | 变更内容 |
|---|---|---|
| 2026-11-10 | v0.1 | 阶段 0 最小壳：ValueDisplay + 运行端窗口 |
| 2026-11-17 | v0.2 | 常用控件集补齐：按钮（五种动作模式）/指示灯/数值输入/进度条 + PointValueParser 输入转换 + 播放器写入接线 |
