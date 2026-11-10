# HMISim

PC 端通用组态软件（对标 MCGS Pro 形态，运行于 PC）。设计依据：《PC 端通用组态软件开发原型书 v0.1》《HMISim 工程编码与文档规范 v0.2》（位于 `~/workspace/pc-scada-project/`）。

当前进度：**阶段 0（骨架）**——内核 + FINS 驱动 + 模拟器 + 控制台演示 + **WPF 运行端最小壳**已落地；自动化测试 **45 项全过**；整个解决方案（含 WPF 项目，以 EnableWindowsTargeting 交叉编译）在 Linux 上编译 0 错误，窗口实际运行请在 Windows 上验证。

## 结构（按规范分级目录）

- `src/Core/HMISim.DriverSdk` — 驱动契约（宪法层）
- `src/Core/HMISim.Core` — 实时数据库 / 需求注册表 / 调度器 / 运行时宿主
- `src/Support/HMISim.Common` — 质量码、值快照、日志抽象
- `src/Drivers/HMISim.Drivers.Fins` — 欧姆龙 FINS (UDP) 驱动
- `simulators/` — FINS 模拟器（类库 + 独立程序）
- `tools/HMISim.DemoHost` — 阶段 0 全链路演示（控制台）
- `src/UI/HMISim.Controls` — UI 组件库（当前含 ValueDisplay 数值显示控件）
- `src/UI/HMISim.Runtime` — 运行端最小壳（值显示 + 质量变色 + 写设定值 + 拔线/恢复演示）
- `tests/HMISim.Tests` — 零依赖断言测试（金帧 / 合块 / 注册表 / 调度 / 模拟器帧）
- `docs/模块/` — 模块设计说明书（与代码同仓同提交）

## 在 Windows 开发机上运行

前置：.NET SDK 10。

```powershell
dotnet build HMISim.slnx
dotnet run --project tests/HMISim.Tests          # 期望结尾：45/45 通过
dotnet run --project src/UI/HMISim.Runtime       # 运行端窗口（推荐先跑这个看效果）
dotnet run --project tools/HMISim.DemoHost        # 控制台版全链路演示
dotnet run --project simulators/HMISim.FinsSimulatorApp   # 独立模拟器（可选）
```

运行端窗口：启动即带进程内模拟器，四个点位卡片实时刷新；可写设定值（D200）、点「模拟拔线」看质量由绿→琥珀→红、点「恢复连接」看回绿。

DemoHost 演示流程：进程内模拟器产活数据（D0 计数器、D2 温度、D100.00 翻转）→ 调度采集打印 → 写 D200=1234 并校验模拟器内存 → 停模拟器模拟拔线，质量 Good → Uncertain → Bad。

## 已知事项

- 阶段 0 框架包零外部依赖（分析器包与 OPC UA 包在立项接入 NuGet 时按 `Directory.Packages.props` 登记启用）。
- FINS 驱动阶段 0 仅支持 D 区；目标框架类库为 net10.0，WPF 的组态端/运行端（net10.0-windows）在阶段 0 后半程加入。
