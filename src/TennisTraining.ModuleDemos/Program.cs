using System;
using System.Threading.Tasks;
using TennisTraining.Core;
using TennisTraining.Vision;
using TennisTraining.Launcher;
using TennisTraining.Projection;
using TennisTraining.Data;
using TennisTraining.Coordination;

Console.OutputEncoding = System.Text.Encoding.UTF8;
string which = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
Console.WriteLine($"==== 网球训练馆 模块自检 ({which}) ====");

switch (which)
{
    case "launcher": await Run(new LauncherControlEngine(new MockLauncherTransport())); break;
    case "vision":
        await Run(new VisionModule(new CameraService(
            new SyntheticCameraSource(320, 180, 60), new ManagedColorBallDetector(1),
            new BallTrackingEngine())));
        break;
    case "data": await Run(new DataModule()); break;
    case "projection": await Run(new ProjectionModule()); break;
    case "coordination": await Run(BuildOrchestrator()); break;
    case "crc": RunCrcCheck(); break;
    case "all":
        await Run(new LauncherControlEngine(new MockLauncherTransport()));
        await Run(new VisionModule(new CameraService(
            new SyntheticCameraSource(320, 180, 60), new ManagedColorBallDetector(1),
            new BallTrackingEngine())));
        await Run(new DataModule());
        await Run(new ProjectionModule());
        await Run(BuildOrchestrator());
        break;
    default:
        Console.WriteLine("用法: dotnet run --project ModuleDemos -- [launcher|vision|data|projection|coordination|crc|all]");
        break;
}

static async Task Run(ITennisModule m)
{
    Console.WriteLine($"\n>>> {m.Name}");
    var r = await m.SelfTestAsync();
    Console.WriteLine($"  {(r.Success ? "[通过]" : "[失败]")} - {r.Message}  ({r.ElapsedMs}ms)");
    foreach (var d in r.Details) Console.WriteLine("    " + d);
    m.Dispose();
}

static TrainingOrchestrator BuildOrchestrator()
{
    var bus = new EventBus();
    var vision = new VisionModule(new CameraService(
        new SyntheticCameraSource(320, 180, 60), new ManagedColorBallDetector(1),
        new BallTrackingEngine(), bus), bus);
    var launcher = new LauncherControlEngine(new MockLauncherTransport(), bus);
    var data = new DataModule(null, bus);
    var proj = new ProjectionModule(640, 360, bus);
    return new TrainingOrchestrator(vision, launcher, proj, data, bus);
}

static void RunCrcCheck()
{
    // 国际标准测试向量：Modbus CRC16 对 "123456789" 的校验值应为 0x4B37
    byte[] ascii = System.Text.Encoding.ASCII.GetBytes("123456789");
    ushort crc = ModbusCrc16.Compute(ascii);
    Console.WriteLine($"\n>>> Modbus CRC16 校验 (标准向量 '123456789' 期望 0x4B37)");
    Console.WriteLine($"  CRC = 0x{crc:X4}  -> {(crc == 0x4B37 ? "[通过]" : "[失败]")}");
    // 真实协议帧示例：设备启动指令 01 06 00 03 00 01 + CRC
    var startFrame = ModbusCrc16.AppendCrc(new byte[] { 0x01, 0x06, 0x00, 0x03, 0x00, 0x01 });
    Console.WriteLine($"  发球机启动帧: {ModbusCrc16.ToHex(startFrame)}  自校验={ModbusCrc16.Validate(startFrame)}");
}

