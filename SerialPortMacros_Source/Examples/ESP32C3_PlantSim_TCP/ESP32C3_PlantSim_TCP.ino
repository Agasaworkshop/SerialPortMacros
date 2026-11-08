/*
  ESP32-C3 SuperMini - simulated first-order system (one pole),
  connected to SerialPortMacros over Wi-Fi / TCP.

      G(s) = K / (tau*s + 1)        exact discretization (ZOH) with Ts = 10 ms

  The ESP is the CLIENT: it connects to the PC, where SerialPortMacros is listening
  (logical port with "wifi" checked, "port" field = TCP_PORT, connect button).
  If the connection drops, it retries by itself every second.

  FRAME SENT (13 bytes, 100 times per second) - same as the serial version:
      AA 55 | 10-byte payload | XOR8
      payload = 77 bits (+3 padding bits), little-endian / LSB first:
          n : i13  sample counter (goes from -4096 to 4095 and wraps)
          y : f32  system output
          u : f32  current system input (last command received)

  COMMAND RECEIVED (text, one line):  "U=1.25\n"  or "1.25\n"

  In SerialPortMacros (port settings -> Data: Binary -> "..."):
      Fields       n:i13 y:f32 u:f32
      Sync header  AA 55
      Byte order   Little-endian
      Checksum     Xor8

  The USB serial port stays free and is used for status messages (115200 baud).
*/

#include <WiFi.h>
#include <math.h>
#include <string.h>
#include <stdlib.h>

// ---- EDIT THESE ----
const char*    WIFI_SSID = "YourWiFiName";
const char*    WIFI_PASS = "YourWiFiPassword";
const IPAddress PC_IP(192, 168, 1, 100);   // IP of the PC running SerialPortMacros (ipconfig)
const uint16_t TCP_PORT  = 5001;           // same number as the "port" field in the program

// ---- Simulated system parameters ----
const float K   = 2.0f;      // static gain
const float TAU = 0.5f;      // time constant [s]
const float TS  = 0.01f;     // sampling period [s]
const uint32_t TS_US = 10000;

// ---- State ----
WiFiClient client;
float a;                     // discrete pole: exp(-Ts/tau)
float y = 0.0f;              // output
float u = 0.0f;              // input (updated by the commands)
uint16_t sample = 0;         // sample counter (13 bits are sent)

char line[32];
uint8_t lineLen = 0;
uint32_t nextTick;
uint32_t lastConnectTry = 0;

// ------------------------------------------------------------------
// Network
// ------------------------------------------------------------------

void connectWifi()
{
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);                   // low and constant latency
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  // Many C3 SuperMini boards have an antenna that fails to connect at full power:
  // lowering it fixes the "never connects" problem.
  WiFi.setTxPower(WIFI_POWER_8_5dBm);

  Serial.print("Wi-Fi");
  while (WiFi.status() != WL_CONNECTED) {
    delay(250);
    Serial.print('.');
  }
  Serial.print(" connected, IP ");
  Serial.println(WiFi.localIP());
}

// Never blocks for more than ~0.5 s: the simulation keeps running without a PC.
void keepConnected()
{
  if (client.connected()) return;
  if (millis() - lastConnectTry < 1000) return;
  lastConnectTry = millis();

  if (WiFi.status() != WL_CONNECTED) {
    WiFi.reconnect();
    return;
  }

  client.stop();
  if (client.connect(PC_IP, TCP_PORT, 500)) {
    client.setNoDelay(true);              // send every frame immediately
    lineLen = 0;
    Serial.println("Connected to SerialPortMacros");
  } else {
    Serial.println("PC not reachable, retrying...");
  }
}

// ------------------------------------------------------------------
// Protocol
// ------------------------------------------------------------------

// Writes the 'nbits' least significant bits of 'value' starting at bit 'pos'
// of the buffer (little-endian / LSB first, as SerialPortMacros reads them).
void putBits(uint8_t* buf, int& pos, uint32_t value, int nbits)
{
  for (int i = 0; i < nbits; i++, pos++)
    if (value & (1UL << i))
      buf[pos >> 3] |= (uint8_t)(1u << (pos & 7));
}

void sendFrame()
{
  if (!client.connected()) return;

  uint32_t yBits, uBits;
  memcpy(&yBits, &y, sizeof(yBits));          // IEEE-754 bits of the floats
  memcpy(&uBits, &u, sizeof(uBits));

  uint8_t frame[13] = { 0 };
  frame[0] = 0xAA;
  frame[1] = 0x55;

  uint8_t* payload = frame + 2;               // 10 bytes = 80 bits, 77 used
  int pos = 0;
  putBits(payload, pos, sample & 0x1FFF, 13); // n : bit  0..12
  putBits(payload, pos, yBits, 32);           // y : bit 13..44
  putBits(payload, pos, uBits, 32);           // u : bit 45..76  (last 3 bits are zero)

  uint8_t x = 0;
  for (int i = 0; i < 10; i++) x ^= payload[i];
  frame[12] = x;                              // XOR8 checksum of the payload

  client.write(frame, sizeof(frame));         // a single TCP packet per frame
}

void readCommands()
{
  while (client.connected() && client.available()) {
    char c = (char)client.read();

    if (c == '\n' || c == '\r') {
      if (lineLen == 0) continue;
      line[lineLen] = '\0';
      lineLen = 0;

      const char* p = line;                   // accepts "U=1.25", "u=1.25", "1.25"
      if ((p[0] == 'U' || p[0] == 'u') && p[1] == '=') p += 2;

      char* end;
      float v = strtof(p, &end);
      if (end != p && isfinite(v)) {
        u = v;
      }
    }
    else if (lineLen < sizeof(line) - 1) {
      line[lineLen++] = c;
    }
    else {
      lineLen = 0;                            // line too long: discard it
    }
  }
}

// ------------------------------------------------------------------

void setup()
{
  Serial.begin(115200);
  delay(500);
  a = expf(-TS / TAU);
  connectWifi();
  nextTick = micros();
}

void loop()
{
  keepConnected();
  readCommands();

  if ((int32_t)(micros() - nextTick) >= 0) {
    nextTick += TS_US;

    // If the loop was stalled (e.g. a connection attempt), do not catch up on all
    // the missed steps at once: restart from now.
    if ((int32_t)(micros() - nextTick) > (int32_t)(10 * TS_US))
      nextTick = micros() + TS_US;

    // y[k+1] = a*y[k] + (1-a)*K*u[k]
    y = a * y + (1.0f - a) * K * u;
    sample++;

    sendFrame();
  }
}
