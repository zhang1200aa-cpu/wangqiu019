# HJSE 网球训练馆系统（Windows 核心功能模块）

基于现有 `HJSE-1001` 网球发球机资料（小程序 `tennisyy` 的 BLE/Modbus 协议、STM32 下位机）实现的 Windows 端核心功能模块。
采用 **C# / .NET 8 / WinForms**，每个功能模块独立成项目、可单独运行自检。

## 解决方案结构

```
TennisTraining/
├── Core            共享：模型/枚举/事件总线/Modbus 协议(CRC16+帧构造)/模块接口
├── Vision          视觉识别与追踪（摄像头源+HSV球检测+轨迹+落点预测）
├── Launcher        发球机控制（Modbus RTU 帧+CRC16+串口/BLE传输+球参数编码）
├── Projection      投影显示与交互（球场/轨迹/落点/热力图/靶点/评分 GDI+渲染）
├── Data            数据记录与分析（SQLite 仓储+性能统计引擎）
├── Coordination    系统控制与协调（训练状态机+端到端编排器）
├── App             Windows 主程序（WinForms，分Tab，每模块可独立自检）
└── ModuleDemos     控制台：命令行单独运行任一模块自检
```

## 发球机通讯协议（已对齐真实设备）

来自 `3 软件/tennisyy/pages/serveControl/serveControl.vue` 与 `utils/util.js`：

| 功能 | Modbus 功能码 | 寄存器 | 取值 |
|---|---|---|---|
| 球参数下发 | 0x10 写多寄存器 | 0x0021 起 | 每球 6 字节=[球速,弧度,左右旋,球频,旋转,0]，每包15球，最多45球 |
| 会话配置 | 0x10 写多寄存器 | 0x001A 起(5寄存器) | [总时间,课程循环数,组循环数,微调,组间隔] |
| 设备控制 | 0x06 写单寄存器 | 0x0003 | 0停止/1开始/2故障/3暂停 |
| 微调 | 0x06 写单寄存器 | 0x001D | 1弧度+/2弧度-/3水平+/4水平-/5球频+/6球频- |
| 故障码查询 | 0x03 读寄存器 | 0x0001 | 0无/1上发球堵转/.../8俯仰故障 |

- CRC：标准 Modbus CRC16（多项式 0xA001，初值 0xFFFF，低字节在前）——已用国际标准向量 "123456789"→0x4B37 验证。
- 球参数编码移植自 `util.js` 的 `processBalls`：左右旋 -15..15→30..60；旋转 上旋{4,3,2}/平击5/下旋{6,7,8}。
- 传输层：默认 `MockLauncherTransport`（无硬件即可自检）；真实硬件用 `SerialLauncherTransport`（RS-232/485 串口，可经蓝牙串口模块透传）。

## 构建与运行

```bash
# 构建整个解决方案（8 个项目）
dotnet build TennisTraining.sln

# 运行 Windows 主程序
dotnet run --project src/TennisTraining.App

# 控制台单独运行某模块自检
dotnet run --project src/TennisTraining.ModuleDemos -- launcher
dotnet run --project src/TennisTraining.ModuleDemos -- vision
dotnet run --project src/TennisTraining.ModuleDemos -- data
dotnet run --project src/TennisTraining.ModuleDemos -- projection
dotnet run --project src/TennisTraining.ModuleDemos -- coordination
dotnet run --project src/TennisTraining.ModuleDemos -- crc
dotnet run --project src/TennisTraining.ModuleDemos -- all
```

## 各模块独立运作说明

- **视觉**：`SyntheticCameraSource` 生成带运动网球的合成帧，`ManagedColorBallDetector` 用 HSV 阈值检测，`BallTrackingEngine` 跨帧追踪并预测落点。无需摄像头即可自检；接入真实摄像头只需实现 `ICameraSource`（可用 OpenCvSharp4 的 `VideoCapture`）。
- **发球机**：`LauncherControlEngine` 通过 `ILauncherTransport` 下发会话配置+球参数+启动指令；Mock 传输记录所有帧并校验 CRC/启动帧，确保协议正确。
- **投影**：`ProjectionModule` 渲染到 `BitmapProjectorView`，可送至第二屏无边框窗体投影或 PictureBox 预览，支持导出 PNG。
- **数据**：`TrainingRepository` 用 SQLite 记录会话/逐球；`AnalysisEngine` 计算命中率/均速/一致性/质量评分。
- **协调**：`TrainingStateMachine` 管理状态流转；`TrainingOrchestrator` 经事件总线串联 摄像头→识别→分析→发球机→投影→记录 的端到端闭环，含丢球容错与故障处理。

## 自检结果（实测）

```
发球机控制   [通过] 5帧/CRC有效/启动帧 010600030001 正确
视觉识别     [通过] 30/30命中, 速度1.01m/s, 落点2.16m
数据记录     [通过] 建表/写入/查询/统计, 命中率60%
投影显示     [通过] 1280x720 渲染, PNG 30KB
系统协调     [通过] 端到端闭环, FSM=Idle, 1会话
Modbus CRC   [通过] 标准向量 0x4B37
```

## 扩展点

- 真实摄像头：实现 `ICameraSource`（推荐 OpenCvSharp4 + `OpenCvSharp4.runtime.win`），或用深度学习 YOLO 实现 `IBallDetector`。
- 真实发球机：`SerialLauncherTransport` 接 RS-232/485；若走原生 BLE，实现 `ILauncherTransport`（Windows BLE WinRT API）。
- 真实投影：将 `ProjectionModule.View.Bitmap` 显示到第二屏的无边框 `Form`。
- 多摄像头 3D 重建：扩展 `TrajectoryCalculator` 的标定与多视几何。
