# HMISim

PC 端通用组态软件（对标 MCGS Pro 形态，运行于 PC）。设计依据：《PC 端通用组态软件开发原型书 v0.1》《HMISim 工程编码与文档规范 v0.2》（位于 `~/workspace/pc-scada-project/`）。

当前进度：**阶段 1 起步（组态闭环）**——工程文档模型 + **组态端（可编辑画布）** + 播放器共用渲染已落地；此前阶段 0 的内核/FINS 驱动/模拟器/运行端最小壳俱在。自动化测试 **56 项全过**；整个解决方案（含全部 WPF 项目，以 EnableWindowsTargeting 交叉编译）在 Linux 上编译 0 错误，窗口实际运行请在 Windows 上验证。

## 结构（按规范分级目录）

- `src/Core/HMISim.DriverSdk` — 驱动契约（宪法层）
- `src/Core/HMISim.Core` — 实时数据库 / 需求注册表 / 调度器 / 运行时宿主
- `src/Support/HMISim.Common` — 质量码、值快照、日志抽象
- `src/Drivers/HMISim.Drivers.Fins` — 欧姆龙 FINS (UDP) 驱动
- `simulators/` — FINS 模拟器（类库 + 独立程序）
- `tools/HMISim.DemoHost` — 阶段 0 全链路演示（控制台）
- `src/UI/HMISim.Controls` — UI 组件库（ValueDisplay 数值显示控件、ScreenPlayer 画面播放器）
- `src/UI/HMISim.Runtime` — 运行端（无参数=演示列表模式；带工程文件参数=工程画面模式）
- `src/UI/HMISim.Designer` — 组态端（工具箱/画布拖拽缩放/属性面板/工程保存打开/预览运行）
- `src/Support/HMISim.ProjectModel` — 工程文档模型与 JSON 存取（设备+点位+画面）
- `tests/HMISim.Tests` — 零依赖断言测试（金帧 / 合块 / 注册表 / 调度 / 模拟器帧）
- `docs/模块/` — 模块设计说明书（与代码同仓同提交）

## 在 Windows 开发机上运行

前置：.NET SDK 10。

```powershell
dotnet build HMISim.slnx
dotnet run --project tests/HMISim.Tests          # 期望结尾：56/56 通过
dotnet run --project src/UI/HMISim.Designer      # 组态端：拖控件、改属性、保存工程、预览运行
dotnet run --project src/UI/HMISim.Runtime       # 运行端演示列表模式
dotnet run --project src/UI/HMISim.Runtime -- C:\path\工程.json   # 运行端工程模式（播放组态端保存的画面）
dotnet run --project tools/HMISim.DemoHost        # 控制台版全链路演示
```

组态端流程：启动即载入样例工程 → 左侧工具箱加「数值显示 / 文本标签」→ 画布拖动、右下角灰块缩放 → 右侧属性面板改标题/绑定点位/几何后点「应用属性」→ 工具栏保存为 .json → 「预览运行」开窗看真实数据与质量变色。

DemoHost 演示流程：进程内模拟器产活数据（D0 计数器、D2 温度、D100.00 翻转）→ 调度采集打印 → 写 D200=1234 并校验模拟器内存 → 停模拟器模拟拔线，质量 Good → Uncertain → Bad。

## 已知事项

- 阶段 0 框架包零外部依赖（分析器包与 OPC UA 包在立项接入 NuGet 时按 `Directory.Packages.props` 登记启用）。
- FINS 驱动阶段 0 仅支持 D 区；目标框架类库为 net10.0，WPF 的组态端/运行端（net10.0-windows）在阶段 0 后半程加入。
