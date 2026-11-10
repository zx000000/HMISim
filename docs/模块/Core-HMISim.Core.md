# 模块设计说明书：Core-HMISim.Core

- **所属分层**：Core
- **对应程序集**：HMISim.Core（另含同层契约程序集 HMISim.DriverSdk、支持库 HMISim.Common）
- **文档版本**：v0.1　**最后更新**：2026-11-10

## 1. 模块概述

运行时内核：实时数据库、需求注册表、每设备调度循环与运行时宿主。不负责：协议编解码（驱动层）、画面（UI 层）、报警/历史评估（Services 层）。

**依赖关系**：依赖 HMISim.DriverSdk、HMISim.Common；被 UI 层与 Services 层依赖。

## 2. 对外契约

| 契约 | 形式 | 说明 |
|---|---|---|
| `RealtimeDatabase.Get/Changed` | 类/事件 | 全系统读变量值的唯一入口；快照为 {值， 时间戳， 质量码} |
| `DemandRegistry.Register/UnregisterSource` | 类 | 消费者登记采集需求，来源维度可整组注销 |
| `RuntimeHost.AddDevice/Start/StopAsync` | 类 | 按工程定义装配设备并启停 |
| `DeviceRuntime.TryEnqueueWrite` | 类 | 写请求入优先队列（有界 1024，满丢最旧） |

DriverSdk 契约（`IDeviceDriver`/`IDeviceSession`）见驱动开发手册要点：通讯失败不抛异常、返回结果+质量信号；宿主保证每设备单循环串行调用驱动。

## 3. 类清单

| 类名 | 所在文件 | 作用（一句话） | 备注 |
|---|---|---|---|
| RuntimeHost | RuntimeHost.cs | 装配全部设备与调度循环的宿主 | AddDevice 时以缺省周期登记 "runtime" 常驻需求 |
| DeviceRuntime | DeviceRuntime.cs | 单台设备的调度循环：到期计算、写优先、回填、失败降级 | 质量判 Bad 阈值 = 连续失败 3 次 |
| DemandRegistry | DemandRegistry.cs | 需求并集注册表：同点位多方需求取最短周期，归零退出采集 | 线程安全 |
| RealtimeDatabase | RealtimeDatabase.cs | 变量当前值的唯一中枢 | 变化事件仅在值/质量实际变化时触发 |
| PointDefinition | PointDefinition.cs | 点位定义（设备+定位串+类型+缺省周期） | 记录类型 |
| ValueSnapshot / Quality | HMISim.Common | 不可变值快照与四态质量码 | 与 OPC UA StatusCode 对齐 |

## 4. 关键类详述

### 4.1 DeviceRuntime

**作用**：把「需求并集」变成实际通讯：每轮先排空写队列，再读到期点位，结果回填实时数据库；单轮异常被捕获退避，循环永不自杀。与 DemandRegistry、RealtimeDatabase、IDeviceSession 协作。

**方法清单**：

| 方法 | 作用 | 参数要点 | 返回值 / 质量约定 | 线程要求 |
|---|---|---|---|---|
| RunAsync | 调度主循环 | CancellationToken 控制停机 | 失败计数达 3 判 Bad，未达为 Uncertain，值冻结 | 单实例单循环 |
| TryEnqueueWrite | 写请求入队 | 点位 Id + CLR 值（类型须匹配点位） | 入队成功与否；写成功立即回填数据库 | 任意线程 |

### 4.2 DemandRegistry

**作用**：通讯量 = 需求并集的裁定者。画面以更短周期登记即加速、关闭按来源注销即回落。

**方法清单**：

| 方法 | 作用 | 参数要点 | 返回值 / 质量约定 | 线程要求 |
|---|---|---|---|---|
| Register | 登记/覆盖一条需求 | 来源、点位、周期（须为正） | — | 任意线程 |
| GetEffectivePeriod | 取生效周期 | 点位 Id | 无人需求返回 null = 不采集 | 任意线程 |

## 5. 约定与注意事项

- 调度循环的休眠取「最近到期点」与 100ms 上限的较小者，写队列入队会信号唤醒，保证写延迟不被读周期绑架。
- 失败日志只记状态迁移点（首次失败、判 Bad），不逐轮输出（演示运行实测后修订）。

## 6. 参考来源

无（调度模型为本项目自研，结构参考 SnCollector 的单设备轮询经验）。

## 7. 变更记录

| 日期 | 版本 | 变更内容 |
|---|---|---|
| 2026-11-10 | v0.1 | 阶段 0 初版：数据库/注册表/调度/宿主落地 |
