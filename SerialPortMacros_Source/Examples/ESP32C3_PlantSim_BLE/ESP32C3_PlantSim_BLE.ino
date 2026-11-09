/*
  ESP32-C3 SuperMini - simulated first-order system (one pole),
  connected to SerialPortMacros over Bluetooth LE (Nordic UART Service).

      G(s) = K / (tau*s + 1)        exact discretization (ZOH) with Ts = 10 ms

  The ESP is a BLE peripheral named DEVICE_NAME that offers the Nordic UART Service:
      service 6E400001-B5A3-F393-E0A9-E50E24DCCA9E
      RX      6E400002-...  the PC writes the commands here
      TX      6E400003-...  the ESP sends the frames here (notifications)

  In SerialPortMacros: set the port mode to "BLE", choose DEVICE_NAME in the list,
  then press the connect button.

  FRAME SENT (13 bytes, 100 times per second) - same as the serial/TCP versions:
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
  Arduino IDE: board "ESP32C3 Dev Module" (fits the default partition scheme).

  Uses the BLE library that comes with the ESP32 core (3.x). An old "ESP32 BLE Arduino"
  library (Kolban, 2.0.0) installed in Documents\Arduino\libraries\BLE takes precedence
  and does not compile with core 3.x: remove or rename that folder.
*/

#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>
#include <math.h>
#include <string.h>
#include <stdlib.h>

// ---- Edit if you like ----
const char* DEVICE_NAME = "SPM-PlantSim";

// ---- Nordic UART Service ----
#define NUS_SERVICE_UUID "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_RX_UUID      "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_TX_UUID      "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"

// ---- Simulated system parameters ----
const float K   = 2.0f;      // static gain
const float TAU = 0.5f;      // time constant [s]
const float TS  = 0.01f;     // sampling period [s]
const uint32_t TS_US = 10000;

// ---- State ----
BLEServer* server = nullptr;
BLECharacteristic* txChar = nullptr;
volatile bool connected = false;
volatile bool restartAdvertising = false;

float a;                     // discrete pole: exp(-Ts/tau)
float y = 0.0f;              // output
volatile float u = 0.0f;     // input (updated by the commands, from the BLE task)
uint16_t sample = 0;         // sample counter (13 bits are sent)

char line[32];
uint8_t lineLen = 0;
uint32_t nextTick;

// ------------------------------------------------------------------
// Commands (called from the BLE task when the PC writes to RX)
// ------------------------------------------------------------------

void handleChar(char c)
{
  if (c == '\n' || c == '\r') {
    if (lineLen == 0) return;
    line[lineLen] = '\0';
    lineLen = 0;

    const char* p = line;                   // accepts "U=1.25", "u=1.25", "1.25"
    if ((p[0] == 'U' || p[0] == 'u') && p[1] == '=') p += 2;

    char* end;
    float v = strtof(p, &end);
    if (end != p && isfinite(v))
      u = v;
  }
  else if (lineLen < sizeof(line) - 1) {
    line[lineLen++] = c;
  }
  else {
    lineLen = 0;                            // line too long: discard it
  }
}

class ServerCallbacks : public BLEServerCallbacks {
  void onConnect(BLEServer* s) override {
    connected = true;
    Serial.println("PC connected");
  }
  void onDisconnect(BLEServer* s) override {
    connected = false;
    restartAdvertising = true;              // advertise again from loop()
    Serial.println("PC disconnected");
  }
};

class RxCallbacks : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* c) override {
    auto value = c->getValue();             // String or std::string, depending on the BLE library version
    for (size_t i = 0; i < value.length(); i++)
      handleChar(value[i]);
  }
};

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
  if (!connected) return;

  float uNow = u;
  uint32_t yBits, uBits;
  memcpy(&yBits, &y, sizeof(yBits));          // IEEE-754 bits of the floats
  memcpy(&uBits, &uNow, sizeof(uBits));

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

  txChar->setValue(frame, sizeof(frame));     // one notification per frame (fits a 20-byte packet)
  txChar->notify();
}

// ------------------------------------------------------------------

void setup()
{
  Serial.begin(115200);
  delay(500);
  a = expf(-TS / TAU);

  BLEDevice::init(DEVICE_NAME);
  server = BLEDevice::createServer();
  server->setCallbacks(new ServerCallbacks());

  BLEService* service = server->createService(NUS_SERVICE_UUID);

  txChar = service->createCharacteristic(NUS_TX_UUID, BLECharacteristic::PROPERTY_NOTIFY);
  txChar->addDescriptor(new BLE2902());       // lets the PC enable notifications

  BLECharacteristic* rxChar = service->createCharacteristic(
      NUS_RX_UUID, BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR);
  rxChar->setCallbacks(new RxCallbacks());

  service->start();

  BLEAdvertising* advertising = BLEDevice::getAdvertising();
  advertising->addServiceUUID(NUS_SERVICE_UUID);  // the PC list marks the device as "UART"
  advertising->setScanResponse(true);             // the name goes in the scan response
  BLEDevice::startAdvertising();

  Serial.print("Advertising as ");
  Serial.println(DEVICE_NAME);
  nextTick = micros();
}

void loop()
{
  if (restartAdvertising) {
    restartAdvertising = false;
    delay(100);
    BLEDevice::startAdvertising();
    Serial.println("Advertising again");
  }

  if ((int32_t)(micros() - nextTick) >= 0) {
    nextTick += TS_US;

    // y[k+1] = a*y[k] + (1-a)*K*u[k]
    y = a * y + (1.0f - a) * K * u;
    sample++;

    sendFrame();
  }
}
