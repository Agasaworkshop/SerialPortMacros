/*
  ESP32-C3 SuperMini - sistema del primo ordine (un polo) simulato,
  collegato a SerialPortMacros via Wi-Fi / TCP.

      G(s) = K / (tau*s + 1)        discretizzato esatto (ZOH) con Ts = 10 ms

  L'ESP è il CLIENT: si collega al PC, dove SerialPortMacros è in ascolto
  (porta logica con la spunta "wifi", campo "port" = TCP_PORT, tasto connetti).
  Se la connessione cade, riprova da solo ogni secondo.

  FRAME INVIATO (13 byte, 100 volte al secondo) - identico alla versione seriale:
      AA 55 | payload 10 byte | XOR8
      payload = 77 bit (+3 di riempimento), little-endian / LSB first:
          n : i13  contatore dei campioni (va da -4096 a 4095 e riparte)
          y : f32  uscita del sistema
          u : f32  ingresso attuale del sistema (ultimo comando ricevuto)

  COMANDO RICEVUTO (testo, una riga):  "U=1.25\n"  oppure "1.25\n"

  In SerialPortMacros (impostazioni della porta -> Data: Binary -> "..."):
      Fields       n:i13 y:f32 u:f32
      Sync header  AA 55
      Byte order   Little-endian
      Checksum     Xor8

  La seriale USB resta libera: la uso per i messaggi di stato (115200 baud).
*/

#include <WiFi.h>
#include <math.h>
#include <string.h>
#include <stdlib.h>

// ---- DA MODIFICARE ----
const char*    WIFI_SSID = "YourWiFiName";
const char*    WIFI_PASS = "YourWiFiPassword";
const IPAddress PC_IP(192, 168, 1, 100);   // IP del PC con SerialPortMacros (ipconfig)
const uint16_t TCP_PORT  = 5001;           // stesso numero del campo "port" nel programma

// ---- Parametri del sistema simulato ----
const float K   = 2.0f;      // guadagno statico
const float TAU = 0.5f;      // costante di tempo [s]
const float TS  = 0.01f;     // periodo di campionamento [s]
const uint32_t TS_US = 10000;

// ---- Stato ----
WiFiClient client;
float a;                     // polo discreto: exp(-Ts/tau)
float y = 0.0f;              // uscita
float u = 0.0f;              // ingresso (aggiornato dai comandi)
uint16_t sample = 0;         // contatore dei campioni (vengono inviati 13 bit)

char line[32];
uint8_t lineLen = 0;
uint32_t nextTick;
uint32_t lastConnectTry = 0;

// ------------------------------------------------------------------
// Rete
// ------------------------------------------------------------------

void connectWifi()
{
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);                   // latenza bassa e costante
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  // Molte C3 SuperMini hanno un'antenna che non si aggancia a piena potenza:
  // ridurla risolve il problema "non si connette mai".
  WiFi.setTxPower(WIFI_POWER_8_5dBm);

  Serial.print("Wi-Fi");
  while (WiFi.status() != WL_CONNECTED) {
    delay(250);
    Serial.print('.');
  }
  Serial.print(" connesso, IP ");
  Serial.println(WiFi.localIP());
}

// Non bloccante per più di ~0.5 s: la simulazione continua anche senza PC.
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
    client.setNoDelay(true);              // invia subito ogni frame
    lineLen = 0;
    Serial.println("Collegato a SerialPortMacros");
  } else {
    Serial.println("PC non raggiungibile, riprovo...");
  }
}

// ------------------------------------------------------------------
// Protocollo
// ------------------------------------------------------------------

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
  if (!client.connected()) return;

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

  client.write(frame, sizeof(frame));         // un solo pacchetto TCP per frame
}

void readCommands()
{
  while (client.connected() && client.available()) {
    char c = (char)client.read();

    if (c == '\n' || c == '\r') {
      if (lineLen == 0) continue;
      line[lineLen] = '\0';
      lineLen = 0;

      const char* p = line;                   // accetta "U=1.25", "u=1.25", "1.25"
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

    // Se il loop è rimasto fermo (es. tentativo di connessione) non recuperare
    // tutti i passi persi in un colpo: riparti da adesso.
    if ((int32_t)(micros() - nextTick) > (int32_t)(10 * TS_US))
      nextTick = micros() + TS_US;

    // y[k+1] = a*y[k] + (1-a)*K*u[k]
    y = a * y + (1.0f - a) * K * u;
    sample++;

    sendFrame();
  }
}
