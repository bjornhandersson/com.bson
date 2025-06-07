# ARM64 Assembly Temperature Sensor

This project demonstrates ARM64 assembly programming with temperature sensor integration using the MAX31855 thermocouple sensor.

## Quick Start

```bash
# Build and run the temperature sensor
make run
```

The program will continuously read temperature values every 100ms and display:

- Temperature readings in format: `XX.XXC` (e.g., `23.45C`)
- Error message: `ERR404` when no sensor is connected

Press `Ctrl+C` to stop the program.

## Project Structure

```
├── src/
│   ├── main.s              # Main temperature sensor program
│   ├── io/
│   │   ├── output.s        # Output operations and buffer management
│   │   └── serial.s        # Serial communication with Arduino bridge (mocked)
│   ├── hardware/
│   │   └── spi.s           # SPI communication for MAX31855
│   └── system/
│       └── exit.s          # System call wrappers
├── examples/
│   ├── temperature_reader.s # Example temperature reading program
│   └── arduino_bridge.ino  # Arduino sketch for hardware bridge
├── docs/
│   └── MAX31855_INTEGRATION.md # Hardware integration guide
├── math.s                  # Mathematical operations
├── add.s                   # Legacy combined program
└── Makefile               # Build configuration
```

## Build Options

```bash
make              # Build temperature sensor (default)
make run          # Build and run temperature sensor
make run-basic    # Run the basic addition program
make clean        # Clean up generated files
```

## Implementation Details

- **100ms refresh rate** - Continuously reads sensor every 100 milliseconds
- **MAX31855 validation** - Proper fault detection and data format validation
- **Error handling** - Displays "ERR404" when sensor faults are detected
- **Mock Arduino bridge** - Simulates realistic sensor behavior without requiring hardware
- **Real serial communication structure** - Can be easily adapted for actual Arduino hardware
- **Temperature range** - Simulates realistic readings between 18-28°C
- **Fault simulation** - Includes open circuit, short to GND, and short to VCC faults
- **Negative temperature support** - Handles negative temperatures correctly

## Hardware Integration

The program uses a mocked Arduino bridge that simulates real MAX31855 thermocouple sensor behavior. For actual hardware integration, see `docs/MAX31855_INTEGRATION.md`.

## Learning Objectives

- ARM64 assembly programming
- Hardware sensor communication
- Real-time data acquisition
- Error handling in assembly
- System calls and timing
