# Windows 程序说明（HJSE 网球训练馆系统）

## 1. 概述
本程序是面向 HJSE-1001 网球发球机的 Windows 端核心功能模块，采用 **C# / .NET 8 / WinForms**，与现有小程序（uni-app）、STM32 下位机通过同一套 Modbus RTU 协议对接。程序将"视觉识别、发球机控制、投影显示、数据记录、系统协调"五大功能拆分为独立项目，每个模块可单独运行自检，无真实硬件时也能完整跑通端到端闭环。

## 2. 解决方案结构
```
TennisTraining/
├── Core            共享层：模型/枚举/事件总线/Modbus协议(CRC16+帧构造)/模块接口
├── Vision          视觉识别与追踪
├── Launcher        发球机控制（Modbus RTU + 串口/BLE传输 + 球参数编码）
├── Projection      投影显示与交互（GDI+ 球场/轨迹/热力图渲染）
├── Data            数据记录与分析（SQLite + 统计引擎）
├── Coordination    系统控制与协调（状态机 + 端到端编排器）
├── App             Windows 主程序（WinForms，分 Tab）
└── ModuleDemos     控制台：命令行单独运行任一模块自检
```
依赖关系：Core 为基础；Vision/Launcher/Projection/Data 依赖 Core；Coordination 依赖全部模块；App 与 ModuleDemos 依赖全部。

## 3. 构建与运行
```bash
dotnet build TennisTraining.sln                      # 构建 8 个项目
dotnet run --project src/TennisTraining.App           # 启动 Windows 主程序
dotnet run --project src/TennisTraining.ModuleDemos -- all          # 全部自检
dotnet run --project src/TennisTraining.ModuleDemos -- launcher     # 单模块自检
# 可选参数: vision | data | projection | coordination | crc | all
```
环境要求：.NET 8 SDK（Windows Desktop 运行时）。

## 4. 核心模块说明

### 4.1 Core（共享层）
- **模型**（`Models.cs`/`Models.Analysis.cs`）：`BallParameter`（球性/球速/球频/弧度/左右旋/旋转/持拍手，字段与小程序 `courseData.js` 对齐）、`LaunchParameter`、`Trajectory`、`ProcessedBallData`、`TrainingStats`、`TrainingSession`、`LauncherStatus`。
- **枚举**（`Enums.cs`）：`SpinType`(1上旋/2平击/3下旋)、`DevicePosition`(1-6 摆放位)、`TrainingMode`、`ServeMode`、`ServeOrder`、`TrainingState`、`LauncherErrorCode`(1-8 故障码，与小程序 `getErrorCodeTable` 一致)。
- **事件总线**（`EventBus.cs`）：线程安全发布/订阅，支持 UI 同步上下文，统一事件名常量（`EventNames`）。
- **Modbus 协议**（`ModbusCrc16.cs`/`ModbusFrames.cs`）：CRC16（多项式 0xA001，低字节在前）、写多寄存器(0x10)、写单寄存器(0x06)、读寄存器(0x03)帧构造、`BallFramePacker`（每球 6 字节、每包 15 球、最多 45 球）。
- **模块接口**（`Interfaces.cs`）：`ITennisModule`（Name/Status/StartAsync/StopAsync/SelfTestAsync）、`ICameraSource`、`IBallDetector`、`ILauncherTransport`、`IProjectorView`。

### 4.2 Vision（视觉识别与追踪）
- `SyntheticCameraSource`：合成摄像头源，生成沿抛物线运动的网球帧（虚拟时钟，自检用）。
- `ManagedColorBallDetector`：基于 HSV 阈值的网球检测（荧光黄 H∈[20,80]），纯托管无 OpenCV 依赖；可被 OpenCvSharp/YOLO 实现替换。
- `BallTrackingEngine` + `TrajectoryCalculator`：跨帧追踪、2D→3D 转换（单摄像头+地面平面假设）、速度计算、抛体落点预测、旋转估算。

### 4.3 Launcher（发球机控制）
- `BallParameterEncoder`：移植自小程序 `util.js processBalls`——左右旋 -15..15→30..60；旋转 上旋{4,3,2}/平击5/下旋{6,7,8}。
- `LauncherCommands`：HJSE 命令集——设备控制(0x06@0x0003)、微调(0x06@0x001D)、会话配置(0x10@0x001A)、球参数(0x10@0x0021)、故障查询(0x03@0x0001)。
- `LauncherControlEngine`：下发流程"会话配置→球参数(3包)→启动"，含心跳查询故障码、暂停/继续/微调、状态事件。
- 传输层：`MockLauncherTransport`（无硬件自检）、`SerialLauncherTransport`（RS-232/485，可经蓝牙串口模块透传）。

### 4.4 Projection（投影显示与交互）
- `TennisCourt`：ITF 标准场地几何与"世界坐标米→屏幕像素"映射。
- `CourtRenderer`：GDI+ 渲染球场/轨迹/落点(命中绿/失误红)/热力图/靶点/评分。
- `BitmapProjectorView`：位图画布，可送第二屏无边框窗体投影或 PictureBox 预览，支持 PNG 导出。

### 4.5 Data（数据记录与分析）
- `TrainingRepository`：SQLite 仓储，`sessions`/`balls` 两表，记录会话与逐球详情。
- `AnalysisEngine`：命中率/均速/最大速度/质量评分/一致性(基于速度变异系数)。

### 4.6 Coordination（系统控制与协调）
- `TrainingStateMachine`：状态机（Idle→WaitingForServe→Serving→Tracking→Analyzing→Recording→...）含合法迁移校验。
- `TrainingOrchestrator`：经事件总线串联 摄像头→识别→分析→发球机→投影→记录 的端到端闭环，丢球容错（轨迹段结束自动结算）、故障处理。

## 5. 主程序界面（App）
Tab 页：总览(日志+全部自检/启停训练)、视觉、发球机(传输选择/球参数/微调)、投影(预览+PNG)、数据(历史列表)、协调(端到端自检)。底部状态栏实时显示各模块状态与训练状态。

## 6. 自检结果（实测）
| 模块 | 结果 |
|---|---|
| 发球机控制 | 通过（5帧/CRC有效/启动帧 `010600030001` 正确）|
| 视觉识别 | 通过（30/30命中, 速度1.01m/s, 落点2.16m）|
| 数据记录 | 通过（建表/写入/查询/统计, 命中率60%）|
| 投影显示 | 通过（1280×720 渲染, PNG 30KB）|
| 系统协调 | 通过（端到端闭环, FSM=Idle, 1会话）|
| Modbus CRC | 通过（标准向量 "123456789"→0x4B37）|

## 7. 扩展点
- 真实摄像头：实现 `ICameraSource`（OpenCvSharp4 `VideoCapture`）或 `IBallDetector`（YOLO/ONNX）。
- 真实发球机：`SerialLauncherTransport` 接 RS-232/485；原生 BLE 需实现 `ILauncherTransport`（Windows BLE WinRT）。
- 真实投影：将 `ProjectionModule.View.Bitmap` 显示到第二屏无边框 `Form`。
- 多摄像头 3D：扩展 `TrajectoryCalculator` 标定与多视几何。
