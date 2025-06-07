# ARM64 Assembly Temperature Sensor

ARM64 assembly program that reads raw hex data from MAX31855 temperature sensors every second.

## Quick Start

```bash
make run
```

## Output

- Raw sensor data: `0xXXXXXXXX` (32-bit MAX31855 data in hex)
- Error message: `ERR_NO_SNR` (when no sensor data available)
- Refresh rate: 1 second

## Build Commands

```bash
make        # Build program
make run    # Build and run
make clean  # Remove build files
```

## Project Structure

```
src/
├── main.s              # Main program
├── io/
│   ├── output.s        # Output functions
│   └── serial.s        # Serial communication
├── hardware/
│   └── spi.s           # MAX31855 sensor interface
└── system/
    └── exit.s          # System calls
```

## Implementation

- Reads raw 32-bit MAX31855 thermocouple data
- Displays complete sensor information in hexadecimal
- Simulates realistic sensor behavior with fault conditions
- 1 second refresh rate for continuous monitoring
