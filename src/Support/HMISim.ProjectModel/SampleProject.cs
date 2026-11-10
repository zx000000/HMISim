namespace HMISim.ProjectModel;

/// <summary>
/// 样例工程工厂：与控制台 DemoHost / 运行端演示一致的设备、点位与一张排好版的画面。
/// 新建工程的起点与离线演示数据源（组态端「新建」即以此为模板，改后另存）。
/// </summary>
public static class SampleProject
{
    /// <summary>创建样例工程（每次返回新实例，可自由编辑）。</summary>
    public static ProjectDocument Create()
    {
        var document = new ProjectDocument
        {
            Name = "演示工程",
            UseEmbeddedSimulator = true,
            Device = new DeviceDefinition
            {
                DeviceId = "PLC1",
                DriverId = "fins",
                Parameters = new Dictionary<string, string>
                {
                    ["Host"] = "127.0.0.1",
                    ["Port"] = "9600",
                    ["LocalPort"] = "9601",
                    ["TimeoutMs"] = "300",
                },
            },
            Points =
            [
                new PointDto { Id = "计数器", DeviceId = "PLC1", Address = "D0", DataType = "UInt16", ScanPeriodMs = 200 },
                new PointDto { Id = "温度", DeviceId = "PLC1", Address = "D2", DataType = "Float32", ScanPeriodMs = 500 },
                new PointDto { Id = "运行信号", DeviceId = "PLC1", Address = "D100.00", DataType = "Bool", ScanPeriodMs = 200 },
                new PointDto { Id = "设定值", DeviceId = "PLC1", Address = "D200", DataType = "UInt16", ScanPeriodMs = 1000 },
            ],
        };
        document.Screen.Controls =
        [
            new ControlItem { Kind = ControlKind.Label, Title = "一号线监控", X = 24, Y = 16, Width = 400, Height = 36 },
            new ControlItem { Kind = ControlKind.ValueDisplay, Title = "计数器", BindingPointId = "计数器", X = 24, Y = 72, Width = 280, Height = 72 },
            new ControlItem { Kind = ControlKind.ValueDisplay, Title = "温度", BindingPointId = "温度", X = 328, Y = 72, Width = 280, Height = 72 },
            new ControlItem { Kind = ControlKind.ValueDisplay, Title = "运行信号", BindingPointId = "运行信号", X = 24, Y = 168, Width = 280, Height = 72 },
            new ControlItem { Kind = ControlKind.ValueDisplay, Title = "设定值", BindingPointId = "设定值", X = 328, Y = 168, Width = 280, Height = 72 },
        ];
        return document;
    }
}
