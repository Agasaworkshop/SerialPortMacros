# SerialPortMacros – struttura e nuove funzioni

## Struttura del codice

| Cartella / file | Contenuto |
|---|---|
| `Form1.cs` | servizi, avvio/chiusura, mappa dei controlli delle 4 porte |
| `Form1.Ports.cs` | connessione seriale/TCP, cambio modalità, impostazioni porta |
| `Form1.Display.cs` | testo a video (decimato), input utente, cronologia, invio |
| `Form1.Logging.cs` | sessioni di log, messaggi di inizio/fine |
| `Form1.Settings.cs` | lettura/scrittura `settings.json` |
| `Form1.Tools.cs` | macro, grafici, sequenze, interfacce |
| `Core/` | pipeline di processing, log, buffer video, impostazioni, utilità comuni |
| `Transport/` | `PortChannel` (una porta logica, seriale o TCP), `TcpServerPort` |
| `Decoding/` | decoder a righe di testo e decoder di frame binari |
| `Macros/` | file macro, motore macro, keypress, macro custom (API + loader) |
| `Sequences/` | esecuzione dei file di sequenza |
| `UI/` | finestra di configurazione del formato binario |

## Flusso dei dati

```
seriale / TCP (byte) ─► decoder della porta ─► coda ─► thread di processing (priorità alta)
                                                         1. valori live (Form7, macro)
                                                         2. log su file
                                                         3. buffer video (solo accodamento)
                                                         4. macro
                                                         5. grafici
                                         UI (timer 50 ms) ◄── buffer video (decimato)
```

Log, macro e grafici ricevono **ogni** messaggio indipendentemente dalla UI.
Se la finestra non riesce a stare al passo, il buffer video tiene solo le ultime
`DisplayMaxPendingLines` righe (default 2000, in `settings.json`) e a video compare
`··· N lines not shown ···`.

## Formato binario

Impostazioni della porta (ingranaggio) → **Data: Binary** → `...`

Campi in ordine di trasmissione: `[nome:]tipo[[n]][*scala][+offset]`

| Tipo | Significato |
|---|---|
| `u5`, `u13`, `u32` | intero senza segno (1..64 bit) |
| `i6`, `i16` (o `s16`) | intero con segno, complemento a 2 (2..64 bit) |
| `b` | un bit |
| `f32`, `f16`, `f64` | float IEEE-754 |
| `x3` | 3 bit da ignorare (padding) |
| `acc:i16[3]` | ripetizione → `acc0 acc1 acc2` |
| `temp:i16*0.01`, `p:u10*0.1-40` | scala e offset |

Esempio: `a:u5 b:u13 c:f32 d:i6` = 56 bit = 7 byte → a video `a=21 b=5000 c=3.14159 d=-17`.

- **Byte order**: *little-endian* (default) se il micro fa `memcpy`/`Serial.write` di una
  struct o variabile (ARM, AVR, ESP32); *big-endian* per l'ordine di rete.
  In little-endian il primo bit del flusso è il bit 0 del primo byte (come i bitfield di GCC).
- **Sync header** (es. `AA 55`): fortemente consigliato, permette di risincronizzarsi se si perde un byte.
- **Checksum** opzionale dopo il payload: Sum8, Xor8, Crc8, Crc16Modbus (calcolato sul solo payload).
- Nel campo **Test (hex)** puoi incollare dei byte e vedere subito la decodifica.

I valori decodificati vengono usati esattamente come i numeri estratti dal testo:
grafici, `{1P1}` nelle interfacce, log, macro.

## Macro custom

Finestra Macros → effetto **Custom code** → nel campo *Code file* il nome del file
(cartella `custom_macros` accanto all'eseguibile). **Edit...** crea il file da un modello
e lo apre. Il file viene (ri)compilato quando premi Play o chiudi la finestra delle macro;
gli errori di compilazione compaiono in rosso nella finestra principale con riga e colonna.

La chiave e le porte della macro decidono quali messaggi arrivano al codice
(`*` = tutti i messaggi delle porte spuntate).

### Forma semplice: (porta, stringa) → (porta, stringa)

```csharp
public class Echo : SimpleMacro
{
    public override (int Port, string Message)? OnMessage(int port, string message)
    {
        if (message.StartsWith("PING")) return (port, "PONG");   // 0 = stessa porta, -1 = tutte
        return null;                                            // niente da inviare
    }
}
```

### Forma completa

```csharp
public class MyMacro : ICustomMacro
{
    IMacroContext ctx;
    public void Start(IMacroContext c) { ctx = c; }          // Play
    public IEnumerable<MacroOutput> Process(MacroInput input)
    {
        // input.Port, input.Message, input.Values (numeri o campi binari), input.Time (s)
        yield return MacroOutput.Line(2, "valore " + input.Values[0]);
    }
    public void Stop() { }                                    // Stop / ricarica
}
```

- Uscite: `MacroOutput.Line(porta, testo)`, `Raw(...)`, `Bytes(porta, byte[])`, `Reply(testo)`.
- `ctx.Variables["x"]` è condiviso con le interfacce (Form7): un pannello può regolare un
  setpoint letto dalla macro, o mostrare un valore calcolato.
- `ctx.Send(...)` si può chiamare anche da un proprio thread/timer (es. loop a frequenza fissa).
- Ogni macro ha la propria istanza: i campi conservano lo stato tra un messaggio e l'altro.
- `Process` gira sul thread di processing, mai in parallelo con sé stesso: deve essere veloce.
- Un file con più classi: `file.cs:NomeClasse`.

### DLL

In alternativa a un `.cs` puoi indicare una `.dll`: progetto *Class Library* `net6.0`
che referenzia `SerialPortMacros.dll` (da `bin\Debug\net6.0-windows`) e contiene una classe
che implementa `ICustomMacro`. Le dipendenze della DLL vanno nella stessa cartella.

## Compatibilità

- `settings.json` del vecchio formato viene convertito automaticamente al primo avvio.
- I file macro esistenti (tipi Keypress e Write back) sono letti come prima.
- Backup dei sorgenti prima del refactor: `_backup_prima_del_refactor.zip`.
