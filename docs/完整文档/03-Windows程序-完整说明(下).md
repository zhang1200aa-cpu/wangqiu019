# 03 Windows 程序完整说明（下）：视觉 / 投影 / 数据 / 协调 / App

## 3.6 Vision 视觉模块
**SyntheticCameraSource.cs**：合成摄像头源，生成沿抛物线运动的网球帧（荧光黄 BGR(0,240,220)，HSV H≈64°），虚拟时钟每帧推进 1/fps 秒，保证时间戳递增。`Start/Stop`(Timer)、`GenerateFrame()`(自检)、`FrameReady` 事件。
**ManagedColorBallDetector.cs**：HSV 阈值检测（H∈[20,80],S≥0.35,V≥0.4），降采样扫描，输出质心+半径+置信度。`IsTennisBall(b,g,r)` 静态判定。可被 OpenCvSharp/YOLO 替换。
**TrajectoryCalculator.cs**：`PixelToWorld`(单摄像头+地面平面假设，由 Height/PitchDeg/Fx/Fy 推算)、`ComputeVelocity`(两点/dt)、`PredictLanding`(抛体运动解二次方程至 Z=0)、`SpeedMps`/`MpsToKmh`。
**BallTrackingEngine.cs**：跨帧维护轨迹点，丢球>500ms 重置，旋转估算(轨迹曲率)。`Process(BallDetectionResult)`→`Trajectory`。
**CameraService.cs**：取帧(摄像头线程)→ConcurrentQueue→消费线程检测+追踪→发布 FrameReceived/BallDetected/TrajectoryUpdated。`SelfTestAsync` 委托 VisionModule。
**VisionModule.cs**：`SelfTestAsync` 生成30帧，验证 30/30命中 + 轨迹≥2点 + 速度>0。

## 3.7 Projection 投影模块
**TennisCourt.cs**：ITF 标准场地(HalfLength=11.885m, SinglesHalfWidth=4.115, DoublesHalfWidth=5.485, ServiceLineX=6.4)。`CourtToScreen(x,y)` 世界米→像素；`GetLines()` 返回边线/底线/发球线/网/中线。
**CourtRenderer.cs**：GDI+ 绘制 `DrawCourt/DrawTrajectory/DrawLandings(命中绿/失误红)/DrawHeatmap/DrawTargets/DrawScore`。
**BitmapProjectorView.cs**：`Bitmap`+`Graphics` 画布，`BeginDraw/EndDraw/Clear`，可送第二屏无边框窗体或 PictureBox。
**ProjectionModule.cs**：`OnTrajectory/OnBallProcessed/OnStats/SetTargets`→`Render()`；`SaveSnapshot(png)`；`SelfTestAsync` 渲染 1280×720 并导出 PNG。

## 3.8 Data 数据模块
**TrainingRepository.cs**(SQLite, Microsoft.Data.Sqlite)：
- 表 `sessions`(id,user_name,mode,position,difficulty,start/end_time,total,hits,misses,avg/max_speed,avg_quality,consistency,duration_ms)；
- 表 `balls`(id,session_id,seq,speed_mps,quality,hit,landing_x/y,hit_x/y/z,spin_type,spin_rpm,timestamp,note)；
- `InsertSession/InsertBall/UpdateSessionStats/GetRecentSessions/CountSessions`。
**AnalysisEngine.cs**：`Compute`(命中率/均速/最大速度/质量/一致性)、`ComputeConsistency`(速度变异系数)、`ScoreBall`(落点偏差+速度)。
**DataModule.cs**：`BeginSession/RecordBall(发布StatsUpdated)/EndSession/History`；`SelfTestAsync` 建表+10球写入+查询+统计。

## 3.9 Coordination 协调模块
**TrainingStateMachine.cs**：9 态合法迁移表(Idle↔WaitingForServe→Serving→Tracking→Analyzing→Recording→...，Paused/Error/Stopped)，`Transition/CanTransition/StateChanged`。
**TrainingOrchestrator.cs**：经 EventBus 订阅 TrajectoryUpdated/LauncherLaunched/LauncherStatus。
- `StartTraining(LaunchParameter)`：BeginSession→WaitingForServe→启动视觉/投影→Serving→Launcher.Launch。
- `OnTrajectory`：实时投影 + 检测轨迹段结束(起点时间戳回跳/点数骤降)→`FinalizeBall`。
- `FinalizeBall`：落点→命中判定(IsInCourt)→评分→Recording→RecordBall+OnBallProcessed→WaitingForServe。
- `OnLauncherStatus`：故障码非0→Error 态+发布 Error。
- `SelfTestAsync`：端到端 2.6s 闭环(合成视觉+Mock发球机+内存数据+投影)，验证 FSM=Idle + 1会话。

## 3.10 App 主程序
`Program.cs`→`MainForm`。TabControl 6 页：总览(日志+全部自检/启停)、视觉(自检/启停)、发球机(传输选择/球性/球速/弧度/左右/球频/连接/发送/微调)、投影(预览+PNG)、数据(自检/历史ListView)、协调(端到端自检/启停)。状态栏实时各模块状态+训练状态。`MainForm.Handlers.cs`：RunSelfTest/RunAllSelfTests/ConnectLauncher(切串口用反射重建引擎)/LaunchOne/StartTraining/StopTraining/RefreshSessions。

## 3.11 自检结果（✓实测全通过）
发球机(5帧/CRC/启动帧)、视觉(30/30命中,1.01m/s,落点2.16m)、数据(60%命中率)、投影(1280×720 PNG)、协调(端到端 FSM=Idle)、CRC(0x4B37)。
