# 03 Windows 程序完整说明（上）：架构 / Core / 发球机

## 3.1 概述
.NET8/WinForms，8 项目，每模块独立自检。与小程序/固件共用 Modbus RTU 协议。

## 3.2 项目结构与依赖
```
Core            共享：模型/枚举/事件总线/Modbus/接口
Vision          →Core  视觉识别与追踪
Launcher        →Core  发球机控制（+System.IO.Ports）
Projection      →Core  投影显示（+UseWindowsForms）
Data            →Core  数据记录（+Microsoft.Data.Sqlite）
Coordination    →Core+Vision+Launcher+Projection+Data  系统协调
App             →全部  WinForms 主程序（WinExe+UseWindowsForms）
ModuleDemos     →全部  控制台自检
```
统一配置 `Directory.Build.props`：net8.0-windows，Nullable disable，ImplicitUsings enable。

## 3.3 构建运行
```bash
dotnet build TennisTraining.sln
dotnet run --project src/TennisTraining.App
dotnet run --project src/TennisTraining.ModuleDemos -- [launcher|vision|data|projection|coordination|crc|all]
```

## 3.4 Core 共享层
**Enums.cs**：`SpinType`(1上旋/2平击/3下旋)、`DevicePosition`(1-6)、`Hand`(1左/2右)、`TrainingMode`、`ServeMode`(1组/2计时/3自由)、`ServeOrder`(1顺序/2随机)、`TrainingState`(9态)、`LauncherErrorCode`(0无/1-8故障)、`ModuleStatus`。
**Models.cs/Models.Analysis.cs**：`BallParameter`(SpinType/LeftRight/Speed/Rate/Height/Hand/Spin/DelayMs)、`LaunchParameter`(Name/Position/Mode/Order/LoopGroupCount/LoopGroupIntervalMs/Difficulty/Balls)、`FrameData`(BGR byte[]+Width/Height/TimestampMs)、`CameraCalibration`(Fx/Fy/Cx/Cy/Height/PitchDeg)、`Point2D/Point3D`、`BallDetectionResult`、`SpinInfo`、`Trajectory`(Points/Velocity/Speed/PredictedLanding/Spin)、`ProcessedBallData`、`TrainingStats`(HitRate/AvgSpeed/Consistency)、`TrainingSession`、`LauncherStatus`。
**EventBus.cs**：线程安全发布订阅，支持 UI 同步上下文；`EventNames` 常量(FrameReceived/BallDetected/TrajectoryUpdated/BallProcessed/StatsUpdated/LauncherStatus/LauncherLaunched/StateChanged/Error/Log)。
**ModbusCrc16.cs**：`Compute`/`AppendCrc`/`Validate`/`ToHex`。
**ModbusFrames.cs**：`ModbusFrameBuilder`(BuildWriteMultipleRegisters/BuildReadHoldingRegisters/BuildWriteSingleRegister)、`BallFramePacker.Pack`(每球6字节、每包15球、最多45球)。
**Interfaces.cs**：`ITennisModule`(Name/Status/StartAsync/StopAsync/SelfTestAsync/Dispose)、`TennisModuleBase`、`ICameraSource`、`IBallDetector`、`ILauncherTransport`、`IProjectorView`、`SelfTestResult`。

## 3.5 Launcher 发球机模块
**BallParameterEncoder.cs**（移植 util.processBalls）：
- `MapLeftRight(-15..15)`→30..60；`MapSpin(SpinType,spinRaw)`：上旋{4,3,2}/平击5/下旋{6,7,8}；`Encode(BallParameter)`→byte[6]。
**LauncherCommands.cs**（HJSE 命令集）：
- 寄存器：`RegDeviceControl=0x0003`、`RegFineTune=0x001D`、`RegSessionConfig=0x001A`、`RegBallParam=0x0021`、`RegFaultCode=0x0001`。
- `BuildDeviceControl(DeviceControl)`、`BuildFineTune(FineTuneType)`、`BuildSessionConfig(...)`、`BuildSessionConfigForMode(ServeMode,...)`、`BuildBallFrames(byte[][])`、`BuildReadFaultCode()`。
- 枚举 `DeviceControl`(Stop0/Start1/Fault2/Pause3)、`FineTuneType`(HeightPlus1..RateMinus6)。
**传输层**：`SerialLauncherTransport`(System.IO.Ports，115200 8N1，`EnumeratePorts`)、`MockLauncherTransport`(记录帧+注入应答)。
**LauncherControlEngine.cs**：
- `Launch(LaunchParameter)`：下发流程 会话配置→球参数(3包，按 Order 顺序/随机)→启动；发布 `LauncherLaunched`。
- `Pause/Resume/Stop/FineTune/SendDeviceControl`；心跳定时器每 2s 查询故障码并发布 `LauncherStatus`。
- `SelfTestAsync`：Mock 传输，验证 发送5帧 + CRC全有效 + 启动帧 `01 06 00 03 00 01`。
