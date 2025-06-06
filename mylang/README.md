# ARM64 Assembly with MAX31855 Integration

A modular ARM64 assembly project demonstrating separation of concerns and hardware integration. Includes both a basic math program and a temperature reader using the MAX31855 thermocouple sensor.

## Project Structure

```
├── math.s                          # Mathematical operations (root level)
├── src/
│   ├── main.s                      # Basic program entry point
│   ├── io/
│   │   ├── output.s                # Console I/O operations
│   │   └── serial.s                # Serial communication for Arduino bridge
│   ├── hardware/
│   │   └── spi.s                   # SPI communication framework
│   └── system/
│       └── exit.s                  # System operations
├── examples/
│   ├── arduino_bridge.ino          # Arduino sketch for MAX31855 bridge
│   └── temperature_reader.s        # Temperature reading program
├── docs/
│   └── MAX31855_INTEGRATION.md     # Hardware integration guide
└── add.s                           # Original monolithic version (reference)
```

## Programs

### 1. Basic Math Program (`add`)

Demonstrates modular assembly architecture:

- Adds 5 + 3 = 8
- Converts to ASCII and prints result
- Clean separation of logic, I/O, and system operations

### 2. Temperature Reader (`temperature_reader`)

Real-world hardware integration:

- Communicates with MAX31855 via Arduino bridge
- Reads thermocouple temperature
- Displays temperature in Celsius
- Demonstrates serial communication in assembly

## Building and Running

### Basic Program

```bash
# Build and run basic math program
make
make run

# Show project structure
make info
```

### Temperature Reader

```bash
# Build temperature reader
make temperature_reader

# Build both programs
make both

# Run temperature reader (requires Arduino setup)
make run-temp

# Get Arduino setup help
make arduino-help
```

## Hardware Setup for Temperature Reader

### Required Components

- Arduino (Uno, Nano, etc.)
- MAX31855 thermocouple amplifier
- K-type thermocouple
- Jumper wires

### Connections

```
MAX31855    →    Arduino
VCC         →    3.3V
GND         →    GND
SCK         →    Pin 13 (SCK)
CS          →    Pin 10
SO (MISO)   →    Pin 12 (MISO)
```

### Setup Steps

1. **Wire the hardware** according to the connection diagram
2. **Upload Arduino sketch**: Flash `examples/arduino_bridge.ino` to your Arduino
3. **Find serial port**: Run `ls /dev/tty.usb*` to find your Arduino's port
4. **Update serial path**: Edit `device_path` in [`src/io/serial.s`](src/io/serial.s:4)
5. **Build and test**: Run `make temperature_reader && make run-temp`

## Architecture Benefits

### Separation of Concerns

- **Logic** ([`math.s`](math.s:1)): Pure mathematical functions
- **I/O** ([`src/io/`](src/io/)): Console and serial communication
- **Hardware** ([`src/hardware/`](src/hardware/)): SPI and sensor interfaces
- **System** ([`src/system/`](src/system/)): OS interaction and program control

### Modularity Benefits

1. **Reusability**: Math functions work with any I/O method
2. **Testability**: Each module can be tested independently
3. **Maintainability**: Changes to hardware don't affect business logic
4. **Scalability**: Easy to add new sensors or communication methods

## Integration Approaches

### 1. Arduino Bridge (Current Implementation)

- **Pros**: Easy to implement, reliable, cross-platform
- **Cons**: Requires Arduino, adds latency
- **Best for**: Learning, prototyping, macOS development

### 2. Direct SPI (Future Enhancement)

- **Pros**: Direct control, better performance
- **Cons**: Platform-specific, requires root access
- **Best for**: Linux embedded systems, production

### 3. Kernel Module (Advanced)

- **Pros**: Best performance, proper kernel integration
- **Cons**: Complex development, kernel-specific
- **Best for**: Embedded systems, high-performance applications

## Files Reference

### Core Assembly Modules

- [`math.s`](math.s:1) - Mathematical operations
- [`src/main.s`](src/main.s:1) - Basic program entry point
- [`src/io/output.s`](src/io/output.s:1) - Console I/O operations
- [`src/io/serial.s`](src/io/serial.s:1) - Serial communication
- [`src/hardware/spi.s`](src/hardware/spi.s:1) - SPI framework
- [`src/system/exit.s`](src/system/exit.s:1) - System operations

### Examples and Documentation

- [`examples/arduino_bridge.ino`](examples/arduino_bridge.ino:1) - Arduino bridge sketch
- [`examples/temperature_reader.s`](examples/temperature_reader.s:1) - Temperature reading program
- [`docs/MAX31855_INTEGRATION.md`](docs/MAX31855_INTEGRATION.md:1) - Integration guide

### Build System

- [`Makefile`](Makefile:1) - Build configuration for both programs

## Requirements

- macOS with Xcode command line tools
- ARM64 processor (Apple Silicon)
- Arduino and MAX31855 (for temperature reader)

## Next Steps

1. **Start with basic program**: `make && make run`
2. **Set up hardware**: Follow Arduino setup guide
3. **Test temperature reader**: `make run-temp`
4. **Explore direct SPI**: See integration guide for advanced options
5. **Add more sensors**: Extend the modular architecture

This project demonstrates how to structure low-level assembly code using clean architecture principles while integrating real-world hardware sensors.
