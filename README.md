# HMISim

PC 端通用组态软件（对标 MCGS Pro 形态，运行于 PC）。设计依据：《PC 端通用组态软件开发原型书 v0.1》《HMISim 工程编码与文档规范 v0.2》（位于 `~/workspace/pc-scada-project/`）。

当前进度：**阶段 0（骨架）**——内核 + FINS 驱动 + 模拟器 + 全链路演示已落地并通过自动化测试（35 项）。

## 结构（按规范分级目录）

- `src/Core/HMISim.DriverSdk` — 驱动契约（宪法层）
- `src/Core/HMISim.Core` — 实时数据库 / 需求注册表 / 调度器 / 运行时宿主
- `src/Support/HMISim.Common` — 质量码、值快照、日志抽象
- `src/Drivers/HMISim.Drivers.Fins` — 欧姆龙 FINS (UDP) 驱动
- `simulators/` — FINS 模拟器（类库 + 独立程序）
- `tools/HMISim.DemoHost` — 阶段 0 全链路演示（控制台）
- `tests/HMISim.Tests` — 零依赖断言测试（金帧 / 合块 / 注册表 / 调度 / 模拟器帧）
- `docs/模块/` — 模块设计说明书（与代码同仓同提交）

## 在 Windows 开发机上运行

前置：.NET SDK 10。

```powershell
dotnet build HMISim.slnx
dotnet run --project tests/HMISim.Tests          # 期望结尾：35/35 通过
dotnet run --project tools/HMISim.DemoHost        # 全链路演示：采集→写回填→拔线降级
dotnet run --project simulators/HMISim.FinsSimulatorApp   # 独立模拟器（可选）
```

DemoHost 演示流程：进程内模拟器产活数据（D0 计数器、D2 温度、D100.00 翻转）→ 调度采集打印 → 写 D200=1234 并校验模拟器内存 → 停模拟器模拟拔线，质量 Good → Uncertain → Bad。

## 已知事项

- 阶段 0 框架包零外部依赖（分析器包与 OPC UA 包在立项接入 NuGet 时按 `Directory.Packages.props` 登记启用）。
- FINS 驱动阶段 0 仅支持 D 区；目标框架类库为 net10.0，WPF 的组态端/运行端（net10.0-windows）在阶段 0 后半程加入。
