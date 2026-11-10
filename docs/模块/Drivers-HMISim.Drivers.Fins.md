# 模块设计说明书：Drivers-HMISim.Drivers.Fins

- **所属分层**：Drivers
- **对应程序集**：HMISim.Drivers.Fins
- **文档版本**：v0.1　**最后更新**：2026-11-10

## 1. 模块概述

欧姆龙 FINS/UDP 驱动：地址解析、帧编解码、合块规划与 UDP 会话。不负责：调度与重连策略（内核）、参数界面（组态端按参数声明自动生成）。

**依赖关系**：依赖 HMISim.DriverSdk、HMISim.Common；被内核经契约调用。

## 2. 对外契约

| 契约 | 形式 | 说明 |
|---|---|---|
| `FinsDriver` (IDeviceDriver) | 类 | DriverId = `fins`；参数：Host/Port/LocalPort/TimeoutMs |
| 地址格式 | 字符串 | `D<字>` 或 `D<字>.<位00-15>`（阶段 0 仅 D 区） |
| 帧行为 | 协议 | 读 0101 / 写 0102；响应解析兼容标准与 HSL Server 双布局 |

## 3. 类清单

| 类名 | 所在文件 | 作用（一句话） | 备注 |
|---|---|---|---|
| FinsDriver | FinsDriver.cs | 驱动元信息与会话工厂 | 参数自声明 |
| FinsUdpSession | FinsUdpSession.cs | UDP 通道持有与批量读写执行 | 超时不判通道损坏，socket 硬错误才判 |
| FinsCodec | FinsCodec.cs | 拼帧/解帧/类型编解码纯函数 | 金帧测试覆盖 |
| FinsBlockPlanner | FinsBlockPlanner.cs | 到期点位合块规划纯函数 | 间隙 ≤2 字捎带合并，单帧 ≤1000 字 |
| FinsAddress | FinsAddress.cs | 地址解析结果（字+可选位） | DM 字区代码 0x82 / 位 0x02 |

## 4. 关键类详述

### 4.1 FinsUdpSession

**作用**：按合块规划逐块发收；块级失败映射为该块全部点位的 Fail 结果（含上下文），不抛异常给调度器。

**方法清单**：

| 方法 | 作用 | 参数要点 | 返回值 / 质量约定 | 线程要求 |
|---|---|---|---|---|
| ConnectAsync | 打开 UDP 通道 | — | UDP 无握手，打开即已连接 | 调度循环串行调用 |
| ReadAsync | 批量读（内部合块） | 到期点位集合 | 每点位一个 PointValue | 同上 |
| WriteAsync | 批量写（逐点位一帧） | 写点位与值 | 同上 | 同上 |

### 4.2 FinsCodec

**作用**：全部协议字节逻辑集中于此，供会话调用与金帧回归。32 位量低字在前；字符串解码去除尾随 `\0`/空格/`\r\n`（扫码数据实测教训）。

## 5. 约定与注意事项

- 响应不校验来源端点与 SID（阶段 0 与 SnCollector 一致）；接入真实产线前评估是否收紧。
- 后续扩展：CIO/W/H 等区、FINS/TCP、单帧上限按机型可配。

## 6. 参考来源

| 项目名 | 仓库地址 | 许可证 | 参考内容（思路/算法/代码） | 用在何处 |
|---|---|---|---|---|
| SnCollector | github.com/zx000000/SnCollector | 权利人自有（putao 本人） | 代码移植：FinsUdpClient 的帧构造、双格式响应解析、Windows UDP 加固 | FinsCodec、FinsUdpSession、FinsAddress |

## 7. 变更记录

| 日期 | 版本 | 变更内容 |
|---|---|---|
| 2026-11-10 | v0.1 | 阶段 0 初版：D 区字/位读写、合块、双格式兼容 |
