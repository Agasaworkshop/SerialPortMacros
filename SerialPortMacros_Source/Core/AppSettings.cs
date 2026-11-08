using System.Text.Json;
using System.Text.Json.Serialization;
using SerialPortMacros.Decoding;

namespace SerialPortMacros.Core;

/// <summary>Settings of a single logical port.</summary>
public sealed class PortSettings
{
    public bool SendEnabled { get; set; }
    public bool LogEnabled { get; set; }
    public bool UseTcp { get; set; }
    public string BaudRate { get; set; } = "9600";
    public int TcpPort { get; set; }
    public int GraphCount { get; set; } = 1;

    public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "One";
    public bool CustomBuffer { get; set; }
    public int ReadBufferSize { get; set; } = 4096;

    /// <summary>How received bytes are interpreted (text lines or binary frames).</summary>
    public PortDataFormat DataFormat { get; set; } = new PortDataFormat();
}

/// <summary>
/// Settings stored in settings.json.
/// The legacy format (check1..check10, BaudRate1..4, ...) is read and migrated
/// automatically into <see cref="Ports"/>; only the new format is written back.
/// </summary>
public sealed class AppSettings
{
    public const int PortCount = 4;

    public PortSettings[] Ports { get; set; } = CreateDefaultPorts();

    public bool LogUserInput { get; set; }
    public bool OpenLogsAfterStop { get; set; }

    [JsonPropertyName("time_button")] public bool TimeStamps { get; set; }
    [JsonPropertyName("locked_button")] public bool AutoScroll { get; set; }
    [JsonPropertyName("line_end_index")] public int LineEndIndex { get; set; }

    [JsonPropertyName("log_message1")] public string LogStartMessage { get; set; } = "";
    [JsonPropertyName("log_message2")] public string LogStopMessage { get; set; } = "";
    [JsonPropertyName("log_check")] public bool LogWaitForEnd { get; set; }

    /// <summary>Maximum number of lines waiting to be displayed (older ones are dropped).</summary>
    public int DisplayMaxPendingLines { get; set; } = 2000;

    private static PortSettings[] CreateDefaultPorts()
    {
        var ports = new PortSettings[PortCount];
        for (int i = 0; i < PortCount; i++)
            ports[i] = new PortSettings { TcpPort = 5001 + i };
        return ports;
    }

    // ------------------------------------------------------------------
    // Load / save
    // ------------------------------------------------------------------

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
            return new AppSettings();

        string json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        settings.Normalize();
        return settings;
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, WriteOptions));
    }

    /// <summary>Migrates the legacy format and fixes missing or invalid values.</summary>
    public void Normalize()
    {
        bool legacy = Ports == null || Ports.Length != PortCount || check1.HasValue || BaudRate1 != null;

        if (Ports == null || Ports.Length != PortCount)
        {
            var fixedPorts = CreateDefaultPorts();
            if (Ports != null)
                for (int i = 0; i < Math.Min(Ports.Length, PortCount); i++)
                    if (Ports[i] != null) fixedPorts[i] = Ports[i];
            Ports = fixedPorts;
        }

        if (legacy)
            MigrateLegacy();

        for (int i = 0; i < PortCount; i++)
        {
            Ports[i] ??= new PortSettings();
            var p = Ports[i];
            if (p.TcpPort is < 1 or > 65535) p.TcpPort = 5001 + i;
            if (string.IsNullOrWhiteSpace(p.BaudRate)) p.BaudRate = "9600";
            if (p.GraphCount < 1) p.GraphCount = 1;
            if (p.DataBits is < 5 or > 8) p.DataBits = 8;
            p.DataFormat ??= new PortDataFormat();
        }

        if (DisplayMaxPendingLines < 100) DisplayMaxPendingLines = 2000;
    }

    private void MigrateLegacy()
    {
        bool[] send = { check1 ?? false, check2 ?? false, check3 ?? false, check4 ?? false };
        // In the old form: checkBox8 = log port 1, 7 = port 2, 6 = port 3, 5 = port 4.
        bool[] log = { check8 ?? false, check7 ?? false, check6 ?? false, check5 ?? false };
        bool?[] tcp = { UseTcp1, UseTcp2, UseTcp3, UseTcp4 };
        int?[] tcpPort = { TcpPort1, TcpPort2, TcpPort3, TcpPort4 };
        string?[] baud = { BaudRate1, BaudRate2, BaudRate3, BaudRate4 };
        int?[] graphs = { graph_num1, graph_num2, graph_num3, graph_num4 };
        string?[] parity = { Parity1, Parity2, Parity3, Parity4 };
        int?[] dataBits = { DataBits1, DataBits2, DataBits3, DataBits4 };
        string?[] stopBits = { StopBits1, StopBits2, StopBits3, StopBits4 };
        bool?[] customBuf = { CustomBuffer1, CustomBuffer2, CustomBuffer3, CustomBuffer4 };
        int?[] bufSize = { ReadBufferSize1, ReadBufferSize2, ReadBufferSize3, ReadBufferSize4 };

        for (int i = 0; i < PortCount; i++)
        {
            var p = Ports[i];
            p.SendEnabled = send[i];
            p.LogEnabled = log[i];
            p.UseTcp = tcp[i] ?? p.UseTcp;
            p.TcpPort = tcpPort[i] ?? p.TcpPort;
            p.BaudRate = baud[i] ?? p.BaudRate;
            p.GraphCount = graphs[i] ?? p.GraphCount;
            p.Parity = parity[i] ?? p.Parity;
            p.DataBits = dataBits[i] ?? p.DataBits;
            p.StopBits = stopBits[i] ?? p.StopBits;
            p.CustomBuffer = customBuf[i] ?? p.CustomBuffer;
            p.ReadBufferSize = bufSize[i] ?? p.ReadBufferSize;
        }

        LogUserInput = check9 ?? LogUserInput;
        OpenLogsAfterStop = check10 ?? OpenLogsAfterStop;

        // Clear the legacy fields so they are not written again.
        check1 = check2 = check3 = check4 = check5 = check6 = check7 = check8 = check9 = check10 = null;
        UseTcp1 = UseTcp2 = UseTcp3 = UseTcp4 = null;
        TcpPort1 = TcpPort2 = TcpPort3 = TcpPort4 = null;
        BaudRate1 = BaudRate2 = BaudRate3 = BaudRate4 = null;
        graph_num1 = graph_num2 = graph_num3 = graph_num4 = null;
        Parity1 = Parity2 = Parity3 = Parity4 = null;
        DataBits1 = DataBits2 = DataBits3 = DataBits4 = null;
        StopBits1 = StopBits2 = StopBits3 = StopBits4 = null;
        CustomBuffer1 = CustomBuffer2 = CustomBuffer3 = CustomBuffer4 = null;
        ReadBufferSize1 = ReadBufferSize2 = ReadBufferSize3 = ReadBufferSize4 = null;
    }

    // ------------------------------------------------------------------
    // Legacy format fields: only read, for the migration.
    // ------------------------------------------------------------------
#pragma warning disable IDE1006 // names must match the legacy JSON
    public bool? check1 { get; set; }
    public bool? check2 { get; set; }
    public bool? check3 { get; set; }
    public bool? check4 { get; set; }
    public bool? check5 { get; set; }
    public bool? check6 { get; set; }
    public bool? check7 { get; set; }
    public bool? check8 { get; set; }
    public bool? check9 { get; set; }
    public bool? check10 { get; set; }
    public string? BaudRate1 { get; set; }
    public string? BaudRate2 { get; set; }
    public string? BaudRate3 { get; set; }
    public string? BaudRate4 { get; set; }
    public bool? UseTcp1 { get; set; }
    public bool? UseTcp2 { get; set; }
    public bool? UseTcp3 { get; set; }
    public bool? UseTcp4 { get; set; }
    public int? TcpPort1 { get; set; }
    public int? TcpPort2 { get; set; }
    public int? TcpPort3 { get; set; }
    public int? TcpPort4 { get; set; }
    public int? graph_num1 { get; set; }
    public int? graph_num2 { get; set; }
    public int? graph_num3 { get; set; }
    public int? graph_num4 { get; set; }
    public string? Parity1 { get; set; }
    public string? Parity2 { get; set; }
    public string? Parity3 { get; set; }
    public string? Parity4 { get; set; }
    public int? DataBits1 { get; set; }
    public int? DataBits2 { get; set; }
    public int? DataBits3 { get; set; }
    public int? DataBits4 { get; set; }
    public string? StopBits1 { get; set; }
    public string? StopBits2 { get; set; }
    public string? StopBits3 { get; set; }
    public string? StopBits4 { get; set; }
    public bool? CustomBuffer1 { get; set; }
    public bool? CustomBuffer2 { get; set; }
    public bool? CustomBuffer3 { get; set; }
    public bool? CustomBuffer4 { get; set; }
    public int? ReadBufferSize1 { get; set; }
    public int? ReadBufferSize2 { get; set; }
    public int? ReadBufferSize3 { get; set; }
    public int? ReadBufferSize4 { get; set; }
#pragma warning restore IDE1006
}
