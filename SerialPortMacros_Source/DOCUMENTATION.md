# SerialPortMacros – structure and features

## Code layout

| Folder / file | Contents |
|---|---|
| `Form1.cs` | services, startup/shutdown, map of the controls of the 4 ports |
| `Form1.Ports.cs` | serial/TCP connection, mode switching, port settings |
| `Form1.Display.cs` | on-screen text (decimated), user input, history, sending |
| `Form1.Logging.cs` | log sessions, start/stop messages |
| `Form1.Settings.cs` | reading/writing `settings.json` |
| `Form1.Tools.cs` | macros, graphs, sequences, interfaces |
| `Form2` | port settings (serial parameters, data format) |
| `Form3` | macro list and editor |
| `Form4` | real-time graph (also merged graphs) |
| `Form5` (`Log`) | log viewer |
| `Form6` | log start/stop messages |
| `Form7.cs` / `.Editor.cs` / `.Runtime.cs` | interfaces: lifecycle and rendering / edit mode / run mode |
| `Form8` | configuration of an interface element |
| `Core/` | processing pipeline, logging, display buffer, settings, log parser, shared helpers |
| `Transport/` | `PortChannel` (one logical port, serial or TCP), `TcpServerPort` |
| `Decoding/` | text-line decoder and binary-frame decoder |
| `Macros/` | macro files, macro engine, keypress, custom macros (API + loader) |
| `Sequences/` | sequence file runner |
| `UI/` | binary format dialog, interface document model (`.spm`) |
| `Examples/` | ESP32-C3 sketches (serial, TCP and BLE) used with the PID example |

## Data flow

```
serial / TCP (bytes) ─► port decoder ─► queue ─► processing thread (above-normal priority)
                                                    1. live values (Form7, macros)
                                                    2. log files
                                                    3. display buffer (enqueue only)
                                                    4. macros
                                                    5. graphs
                                    UI (50 ms timer) ◄── display buffer (decimated)
```

Logging, macros and graphs receive **every** message regardless of the UI.
If the window cannot keep up, the display buffer keeps only the latest
`DisplayMaxPendingLines` lines (default 2000, in `settings.json`) and the text shows
`··· N lines not shown ···`.

## Port modes: Serial, Wi-Fi (TCP), BLE

Each port has a mode selector under its value box:

- **Serial**: choose the COM port, set the baud rate, connect.
- **Wi-Fi**: the program listens as a TCP server on the given port; the device connects to it.
- **BLE**: Bluetooth LE with the **Nordic UART Service** (service `6E400001-…`, RX `6E400002-…`
  written by the PC, TX `6E400003-…` notified by the device). Choosing BLE opens a window
  listing the nearby devices; the chosen one appears in the port list (click the list to
  change it). The connect button connects / disconnects; while connected, Windows reconnects
  automatically if the link drops. Received bytes go through the same text/binary decoder as
  the other modes, and macros can reply on the BLE port like on any other port.

BLE requires Windows 10 1809 or later with a Bluetooth LE adapter.

## Interface editor (snapping)

While dragging or resizing an element:

- edges and centers snap to the other elements (blue guides) and to the panel edges,
  10 px margins and center lines (gray guides);
- equal spacing: the element snaps to a gap already used between two other elements, or
  to the middle between two neighbours (magenta measures);
- resizing also snaps to the width/height of another element (red measures);
- hold **Shift** to move freely without snapping;
- click an element to select it (orange frame), then use the **arrow keys** to move it by
  1 px (**Shift + arrows**: 10 px).

The rules are in `UI/SnapEngine.cs`.

## Binary format

Port settings (gear button) → **Data: Binary** → `...`

Fields in transmission order: `[name:]type[[n]][*scale][+offset]`

| Type | Meaning |
|---|---|
| `u5`, `u13`, `u32` | unsigned integer (1..64 bits) |
| `i6`, `i16` (or `s16`) | signed integer, two's complement (2..64 bits) |
| `b` | one bit |
| `f32`, `f16`, `f64` | IEEE-754 float |
| `x3` | 3 bits to skip (padding) |
| `acc:i16[3]` | repetition → `acc0 acc1 acc2` |
| `temp:i16*0.01`, `p:u10*0.1-40` | scale and offset |

Example: `a:u5 b:u13 c:f32 d:i6` = 56 bits = 7 bytes → shown as `a=21 b=5000 c=3.14159 d=-17`.

- **Byte order**: *little-endian* (default) when the micro does a `memcpy`/`Serial.write`
  of a struct or variable (ARM, AVR, ESP32); *big-endian* for network order.
  In little-endian mode the first stream bit is bit 0 of the first byte (like GCC bit fields).
- **Sync header** (e.g. `AA 55`): strongly recommended, it lets the decoder resynchronize
  after a lost byte.
- Optional **checksum** after the payload: Sum8, Xor8, Crc8, Crc16Modbus (computed on the payload only).
- In the **Test (hex)** field you can paste bytes and see the decoded result immediately.

Decoded values are used exactly like the numbers parsed from text: graphs,
`{1P1}` in interfaces, logs, macros.

## Custom macros

Macros window → effect **Custom code** → in *Code file* enter the file name
(folder `custom_macros` next to the executable), or pick it with the **...** button.
The **E** button creates the file from a template and opens it.
The file is (re)compiled when you press Play or close the macros window; compilation
errors are shown in red in the main window with line and column.
A compiled `.dll` can be used instead of a `.cs` file.

The macro key and its trigger ports decide which messages reach the code
(`*` = every message from the checked ports). The code itself decides where to
write: any port, several ports, or none.

### Simple form: (port, string) → (port, string)

```csharp
public class Echo : SimpleMacro
{
    public override (int Port, string Message)? OnMessage(int port, string message)
    {
        if (message.StartsWith("PING")) return (port, "PONG");   // 0 = same port, -1 = all
        return null;                                            // nothing to send
    }
}
```

### Full form

```csharp
public class MyMacro : ICustomMacro
{
    IMacroContext ctx;
    public void Start(IMacroContext c) { ctx = c; }          // Play
    public IEnumerable<MacroOutput> Process(MacroInput input)
    {
        // input.Port, input.Source, input.Message, input.Values (numbers or binary fields), input.Time (s)
        if (input.Port == 1)
            yield return MacroOutput.Line(3, "from 1: " + input.Message);   // port 1 -> port 3
        yield return MacroOutput.Reply("ok");                               // back to the source port
    }
    public void Stop() { }                                    // Stop / reload
}
```

- Outputs: `MacroOutput.Line(port, text)`, `Raw(...)`, `Bytes(port, byte[])`, `Reply(text)`.
- **State**: class fields keep their values between messages (e.g. a PID integral).
  Each macro has its own instance; state is reset when the macro is reloaded
  (Play, closing the macros window). `ctx.Variables` survives reloads.
- `ctx.Variables["x"]` is shared with the interfaces (Form7): a panel can set a setpoint
  read by the macro, or show a value computed by it.
- `ctx.Send(...)` can also be called from your own thread/timer (e.g. a fixed-rate loop).
- `Process` runs on the processing thread, never in parallel with itself: keep it fast.
- A file with several classes: `file.cs:ClassName`.

### DLL

Instead of a `.cs` file you can use a `.dll`: a `net6.0` *Class Library* project that
references `SerialPortMacros.dll` (from `bin\Debug\net6.0-windows`) and contains a class
implementing `ICustomMacro`. Put the DLL's dependencies in the same folder.
Speed is the same as a `.cs` file: both are compiled to the same code once loaded.

## PID example (ESP32-C3)

- `Examples/ESP32C3_PlantSim` (USB serial), `Examples/ESP32C3_PlantSim_TCP` (Wi-Fi) and
  `Examples/ESP32C3_PlantSim_BLE` (Bluetooth LE)
  simulate a one-pole system and send a 13-byte frame 100 times per second.
- Binary format: Fields `n:i13 y:f32 u:f32`, header `AA 55`, little-endian, Xor8.
- `custom_macros/Pid.cs` reads `y`, computes the command and replies `U=<value>`.
  Setpoint and gains are the variables `setpoint`, `Kp`, `Ki`, `Kd`.

## Compatibility

- A `settings.json` in the old format is converted automatically on first start.
- Existing macro files (Keypress and Write back) are read as before.
- Source backups: `_backup_prima_del_refactor.zip` (before the refactor),
  `_backup_before_english.zip` (before the English translation).
