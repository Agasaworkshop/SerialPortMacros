/*
  ESP32-C3 SuperMini - simulazione di un sistema del primo ordine (un polo)
  che trasmette lo stato in binario a SerialPortMacros.

      G(s) = K / (tau*s + 1)        discretizzato esatto (ZOH) con Ts = 10 ms

  FRAME INVIATO (13 byte, 100 volte al secondo):
      AA 55 | payload 10 byte | XOR8
      payload = 77 bit (+3 di riempimento), little-endian / LSB first:
          n : i13  contatore dei campioni (va da -4096 a 4095 e riparte)
          y : f32  uscita del sistema
          u : f32  ingresso attuale del sistema (ultimo comando ricevuto)

  COMANDO RICEVUTO (testo, una riga):  "U=1.25\n"  oppure semplicemente "1.25\n"

  In SerialPortMacros (impostazioni della porta -> Data: Binary -> "..."):
      Fields       n:i13 y:f32 u:f32
      Sync header  AA 55
      Byte order   Little-endian
      Checksum     Xor8

  Arduino IDE: scheda "ESP32C3 Dev Module", Tools -> USB CDC On Boot: Enabled
  (altrimenti Serial esce sui pin UART e non sulla USB).
  Non usare Serial.print per il debug: i byte di testo si mescolerebbero ai frame
  (il programma si risincronizza da solo, ma perderesti dei campioni).
*/

#include <math.h>
#include <string.h>
#include <stdlib.h>

// ---- Parametri del sistema simulato ----
const float K   = 2.0f;     // guadagno statico
const float TAU = 0.5f;     // costante di tempo [s]
const float TS  = 0.01f;    // periodo di campionamento [s]
const uint32_t TS_US = 10000;

// ---- Stato ----
float a;                    // polo discreto: exp(-Ts/tau)
float y = 0.0f;             // uscita
float u = 0.0f;             // ingresso (aggiornato dai comandi)
uint16_t sample = 0;        // contatore dei campioni (vengono inviati 13 bit)

char line[32];
uint8_t lineLen = 0;
uint32_t nextTick;

// Scrive i 'nbits' bit meno significativi di 'value' a partire dal bit 'pos'
// del buffer (little-endian / LSB first, come li legge SerialPortMacros).
void putBits(uint8_t* buf, int& pos, uint32_t value, int nbits)
{
  for (int i = 0; i < nbits; i++, pos++)
    if (value & (1UL << i))
      buf[pos >> 3] |= (uint8_t)(1u << (pos & 7));
}

void sendFrame()
{
  uint32_t yBits, uBits;
  memcpy(&yBits, &y, sizeof(yBits));          // bit IEEE-754 dei float
  memcpy(&uBits, &u, sizeof(uBits));

  uint8_t frame[13] = { 0 };
  frame[0] = 0xAA;
  frame[1] = 0x55;

  uint8_t* payload = frame + 2;               // 10 byte = 80 bit, 77 usati
  int pos = 0;
  putBits(payload, pos, sample & 0x1FFF, 13); // n : bit  0..12
  putBits(payload, pos, yBits, 32);           // y : bit 13..44
  putBits(payload, pos, uBits, 32);           // u : bit 45..76  (3 bit finali a zero)

  uint8_t x = 0;
  for (int i = 0; i < 10; i++) x ^= payload[i];
  frame[12] = x;                              // checksum XOR8 del payload

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

      // Accetta "U=1.25", "u=1.25" o "1.25"
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
      lineLen = 0;                            // riga troppo lunga: scartala
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
