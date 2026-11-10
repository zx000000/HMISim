# 模块设计说明书：Support-HMISim.ProjectModel 与 UI-HMISim.Designer

- **所属分层**：Support（ProjectModel）/ UI（Designer 组态端）
- **对应程序集**：HMISim.ProjectModel、HMISim.Designer
- **文档版本**：v0.1　**最后更新**：2026-11-10

## 1. 模块概述

ProjectModel：工程文档的纯数据模型与 JSON 存取——一份工程 = 设备 + 点位表 + 一张画面（阶段 1 最小集），不依赖任何 UI 与运行时。Designer：可视化组态端——工具箱加控件、画布拖拽/缩放、属性面板编辑、工程保存/打开、进程内预览（复用 Controls.ScreenPlayer）。

**依赖关系**：ProjectModel 只依赖 .NET BCL；Designer 依赖 ProjectModel / Controls / Common，经 ScreenPlayer 间接使用 Core 与驱动。

## 2. 对外契约

| 契约 | 形式 | 说明 |
|---|---|---|
| `ProjectDocument` | 类 | 工程根对象：SchemaVersion / Name / MinRuntimeVersion / UseEmbeddedSimulator / Device / Points / Screen |
| `ProjectFile.Save/Load` | 静态类 | JSON（camelCase、枚举字符串）；版本高于支持值拒绝打开并报错 |
| `SampleProject.Create()` | 静态类 | 样例工程（组态端新建模板，与演示点位一致） |
| ScreenPlayer 播放 | Controls 控件 | 组态预览与运行端共用同一播放器，编辑所见即运行所得 |
| 工程→运行时映射 | Controls.ProjectMapper | ProjectModel 与 Core 之间唯一的翻译点 |

## 3. 类清单

| 类名 | 所在文件 | 作用（一句话） | 备注 |
|---|---|---|---|
| ProjectDocument / ScreenDefinition / ControlItem / DeviceDefinition / PointDto | ProjectModel/ProjectDocument.cs | 工程数据模型（POCO） | 控件坐标为画面逻辑坐标 |
| ProjectFile | ProjectModel/ProjectFile.cs | 工程 JSON 读写与版本校验 | 只向前兼容 |
| DesignerViewModel | Designer/ViewModels/DesignerViewModel.cs | 文档状态 + 属性编辑缓冲 | 缓冲经「应用」写回模型 |
| EditorCanvas | Designer/Views/EditorCanvas.xaml(.cs) | 可编辑画布（选中/拖动/缩放手势） | 几何直接写回 ControlItem |

## 4. 关键类详述

### 4.1 EditorCanvas

**作用**：把画面控件渲染为 Border 包裹的预览块（ValueDisplay 用演示值呈现最终观感），处理全部编辑手势并以事件通知宿主。

**方法清单**：

| 方法 | 作用 | 参数要点 | 返回值 / 质量约定 | 线程要求 |
|---|---|---|---|---|
| SetDocument / Rebuild | 装载文档并全量重绘 | 文档对象 | — | UI 线程 |
| RefreshItem | 单控件重呈现 | 属性编辑后的控件 | — | UI 线程 |
| Select | 程序化选中 | 控件或 null | 触发 SelectionChanged | UI 线程 |

### 4.2 DesignerViewModel

**方法清单（关键）**：NewDocument / LoadFrom / SaveTo（文档生命周期）；AddControl / DeleteSelected（工具箱）；SelectItem / ApplyEdits（属性缓冲装载与写回）。

## 5. 约定与注意事项

- 阶段 1 已知取舍（后续阶段处理）：无撤销/重做；属性编辑为「缓冲+应用」模式而非实时双向；单设备单画面；点位/设备编辑界面未做（先用样例点位表）；预览在组态进程内开窗播放，与独立运行端共用 ScreenPlayer。
- 画布尺寸 = 画面逻辑尺寸（样例 960×600），暂不做缩放适配（Viewbox 留待画面系统完善时加）。

## 6. 参考来源

无。

## 7. 变更记录

| 日期 | 版本 | 变更内容 |
|---|---|---|
| 2026-11-10 | v0.1 | 阶段 1 初版：工程模型 + 组态端最小可编辑闭环 |
