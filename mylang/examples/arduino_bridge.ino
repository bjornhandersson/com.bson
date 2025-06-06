// Arduino Bridge for MAX31855 Communication
// This Arduino sketch acts as a bridge between your ARM64 assembly program
// and the MAX31855 thermocouple sensor

#include <SPI.h>

// MAX31855 connections
const int CS_PIN = 10; // Chip Select pin

// Commands from ARM64 program
const char CMD_READ_TEMP = 'T';
const char CMD_READ_RAW = 'R';
const char CMD_STATUS = 'S';

void setup()
{
    Serial.begin(115200);
    SPI.begin();
    pinMode(CS_PIN, OUTPUT);
    digitalWrite(CS_PIN, HIGH); // CS idle high

    Serial.println("MAX31855 Bridge Ready");
}

void loop()
{
    if (Serial.available())
    {
        char command = Serial.read();

        switch (command)
        {
        case CMD_READ_TEMP:
            readTemperature();
            break;

        case CMD_READ_RAW:
            readRawData();
            break;

        case CMD_STATUS:
            checkStatus();
            break;

        default:
            Serial.println("ERROR: Unknown command");
            break;
        }
    }
}

void readTemperature()
{
    uint32_t rawData = readMAX31855();

    // Check for faults
    if (rawData & 0x7)
    {
        Serial.println("ERROR: Sensor fault detected");
        return;
    }

    // Extract temperature (bits 31-18)
    int16_t temp = (rawData >> 18) & 0x3FFF;

    // Handle sign extension
    if (temp & 0x2000)
    {
        temp |= 0xC000; // Sign extend
    }

    // Convert to actual temperature (0.25°C resolution)
    float temperature = temp * 0.25;

    Serial.print("TEMP:");
    Serial.println(temperature, 2);
}

void readRawData()
{
    uint32_t rawData = readMAX31855();
    Serial.print("RAW:0x");
    Serial.println(rawData, HEX);
}

void checkStatus()
{
    uint32_t rawData = readMAX31855();

    Serial.print("STATUS:");
    if (rawData & 0x1)
        Serial.print("OC "); // Open Circuit
    if (rawData & 0x2)
        Serial.print("SCG "); // Short to Ground
    if (rawData & 0x4)
        Serial.print("SCV "); // Short to VCC
    if (!(rawData & 0x7))
        Serial.print("OK"); // No faults
    Serial.println();
}

uint32_t readMAX31855()
{
    uint32_t data = 0;

    digitalWrite(CS_PIN, LOW); // Start communication
    delayMicroseconds(1);      // CS setup time

    // Read 32 bits
    for (int i = 31; i >= 0; i--)
    {
        digitalWrite(SCK, HIGH);
        delayMicroseconds(1);

        if (digitalRead(MISO))
        {
            data |= (1UL << i);
        }

        digitalWrite(SCK, LOW);
        delayMicroseconds(1);
    }

    digitalWrite(CS_PIN, HIGH); // End communication
    delayMicroseconds(1);

    return data;
}