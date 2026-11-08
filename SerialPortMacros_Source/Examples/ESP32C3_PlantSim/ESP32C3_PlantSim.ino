/*
  ESP32-C3 SuperMini - simulation of a first-order system (one pole)
  that sends its state in binary to SerialPortMacros over the USB serial port.

      G(s) = K / (tau*s + 1)        exact discretization (ZOH) with Ts = 10 ms

  FRAME SENT (13 bytes, 100 times per second):
      AA 55 | 10-byte payload | XOR8
      payload = 77 bits (+3 padding bits), little-endian / LSB first:
          n : i13  sample counter (goes from -4096 to 4095 and wraps)
          y : f32  system output
          u : f32  current system input (last command received)

  COMMAND RECEIVED (text, one line):  "U=1.25\n"  or simply "1.25\n"

  In SerialPortMacros (port settings -> Data: Binary -> "..."):
      Fields       n:i13 y:f32 u:f32
      Sync header  AA 55
      Byte order   Little-endian
      Checksum     Xor8

  Arduino IDE: board "ESP32C3 Dev Module", Tools -> USB CDC On Boot: Enabled
  (otherwise Serial goes to the UART pins instead of USB).
  Do not use Serial.print for debugging: the text bytes would mix with the frames
  (the program resynchronizes by itself, but samples would be lost).
*/

#include <math.h>
#include <string.h>
#include <stdlib.h>

// ---- Simulated system parameters ----
const float K   = 2.0f;     // static gain
const float TAU = 0.5f;     // time constant [s]
const float TS  = 0.01f;    // sampling period [s]
const uint32_t TS_US = 10000;

// ---- State ----
float a;                    // discrete pole: exp(-Ts/tau)
float y = 0.0f;             // output
float u = 0.0f;             // input (updated by the commands)
uint16_t sample = 0;        // sample counter (13 bits are sent)

char line[32];
uint8_t lineLen = 0;
uint32_t nextTick;

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

  Serial.write(frame, sizeof(frame));
}

void readCommands()
{
  while (Serial.available()) {
    char c = (char)Serial.read();

    if (c == '\n' || c == '\r') {
      if (lineLen == 0) continue;
      line[lineLen] = '\0';
      lineLen = 0;

      // Accepts "U=1.25", "u=1.25" or "1.25"
      const char* p = line;
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

void setup()
{
  Serial.begin(115200);
  a = expf(-TS / TAU);
  nextTick = micros();
}

void loop()
{
  readCommands();

  if ((int32_t)(micros() - nextTick) >= 0) {
    nextTick += TS_US;

    // y[k+1] = a*y[k] + (1-a)*K*u[k]
    y = a * y + (1.0f - a) * K * u;
    sample++;

    sendFrame();
  }
}
