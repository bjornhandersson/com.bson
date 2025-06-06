# MAX31855 Integration Guide

## Overview

The MAX31855 is a thermocouple-to-digital converter that communicates via SPI. This guide explains how to integrate it with your ARM64 assembly project.

## Hardware Requirements

- MAX31855 breakout board
- Thermocouple (K-type recommended)
- ARM64 system with GPIO pins (Raspberry Pi, etc.)
- Jumper wires

## Connection Diagram

```
MAX31855    →    ARM64 System
VCC         →    3.3V
GND         →    Ground
SCK         →    GPIO 11 (SPI Clock)
CS          →    GPIO 8  (Chip Select)
SO (MISO)   →    GPIO 9  (Master In Slave Out)
```

## Integration Approaches

### 1. User-Space GPIO (Bit-banging)

**Pros**: Full control, educational
**Cons**: Timing sensitive, requires root access

```assembly
// Bit-bang SPI implementation
spi_read_bit:
    // Set clock high
    // Read MISO pin
    // Set clock low
    // Return bit value
```

### 2. Linux SPI Driver

**Pros**: Kernel handles timing, more reliable
**Cons**: Requires device tree configuration

```bash
# Enable SPI in /boot/config.txt
dtparam=spi=on

# Device appears as /dev/spidev0.0
```

### 3. Kernel Module (Advanced)

**Pros**: Best performance, proper kernel integration
**Cons**: Complex, requires kernel development knowledge

## Implementation Steps

### Step 1: Choose Your Platform

- **Raspberry Pi**: Use `/dev/gpiomem` for GPIO access
- **macOS**: Use USB-to-SPI adapter or Arduino bridge
- **Linux PC**: Use USB-to-SPI converter

### Step 2: System Calls Needed

```assembly
// File operations
.equ SYS_OPEN,  5
.equ SYS_READ,  3
.equ SYS_WRITE, 4
.equ SYS_CLOSE, 6
.equ SYS_MMAP,  9

// GPIO memory mapping
.equ PROT_READ,  1
.equ PROT_WRITE, 2
.equ MAP_SHARED, 1
```

### Step 3: Timing Requirements

MAX31855 specifications:

- Clock frequency: Up to 5 MHz
- CS setup time: 100ns minimum
- Data valid time: 100ns after clock edge

### Step 4: Data Format

```
Bit 31    : Sign bit (0=positive, 1=negative)
Bits 30-18: Thermocouple temperature (14 bits)
Bit 17    : Reserved
Bit 16    : Fault bit
Bits 15-4 : Internal temperature (12 bits)
Bit 3     : Reserved
Bit 2     : SCV fault (short to VCC)
Bit 1     : SCG fault (short to GND)
Bit 0     : OC fault (open circuit)
```

## Example Usage in Your Project

### Updated main.s

```assembly
_main:
    // Initialize SPI
    bl init_spi
    cmp x0, 0
    bne error_exit

    // Read temperature
    bl read_max31855
    cmp x0, -1
    beq error_exit

    // Parse temperature
    bl parse_temperature

    // Convert to ASCII and print
    bl convert_to_ascii
    bl store_in_buffer
    bl print_buffer
    bl print_newline

    bl exit_program
```

## Development Recommendations

### For Learning (Start Here)

1. Use Arduino as SPI bridge
2. Send commands via serial from your ARM64 program
3. Arduino handles MAX31855 communication

### For Production

1. Use Linux SPI driver (`/dev/spidev0.0`)
2. Memory map GPIO for chip select
3. Handle all error conditions

### For Embedded Systems

1. Write kernel module
2. Create device driver
3. Use proper interrupt handling

## Testing Strategy

1. Start with dummy data (current implementation)
2. Add Arduino bridge for real sensor data
3. Implement direct SPI when ready
4. Add error handling and fault detection

## Next Steps

1. Choose your hardware platform
2. Decide on integration approach
3. Implement step by step
4. Test with known temperature sources
