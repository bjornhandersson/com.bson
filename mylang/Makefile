# Makefile for ARM64 assembly program with modular structure and hardware integration
TARGET = add
TEMP_TARGET = temperature_reader
MAIN_TEMP_TARGET = main_temperature

# Source files for basic program
MAIN_SOURCE = src/main.s
MATH_SOURCE = math.s
IO_SOURCE = src/io/output.s
SYSTEM_SOURCE = src/system/exit.s

# Source files for temperature reader
TEMP_MAIN_SOURCE = examples/temperature_reader.s
SERIAL_SOURCE = src/io/serial.s
HARDWARE_SOURCE = src/hardware/spi.s

# Object files
BASIC_SOURCES = $(MAIN_SOURCE) $(MATH_SOURCE) $(IO_SOURCE) $(SYSTEM_SOURCE)
BASIC_OBJECTS = $(BASIC_SOURCES:.s=.o)

TEMP_SOURCES = $(TEMP_MAIN_SOURCE) $(MATH_SOURCE) $(IO_SOURCE) $(SYSTEM_SOURCE) $(SERIAL_SOURCE) $(HARDWARE_SOURCE)
TEMP_OBJECTS = $(TEMP_SOURCES:.s=.o)

# Source files for main temperature program
MAIN_TEMP_SOURCES = $(MAIN_SOURCE) $(MATH_SOURCE) $(IO_SOURCE) $(SYSTEM_SOURCE) $(SERIAL_SOURCE) $(HARDWARE_SOURCE)
MAIN_TEMP_OBJECTS = $(MAIN_TEMP_SOURCES:.s=.o)

# Default target - build temperature sensor
all: $(MAIN_TEMP_TARGET)

# Build the basic program
$(TARGET): $(BASIC_OBJECTS)
	ld -arch arm64 -o $(TARGET) $(BASIC_OBJECTS) -lSystem -syslibroot `xcrun --show-sdk-path` -e _main

# Build the temperature reader
$(TEMP_TARGET): $(TEMP_OBJECTS)
	ld -arch arm64 -o $(TEMP_TARGET) $(TEMP_OBJECTS) -lSystem -syslibroot `xcrun --show-sdk-path` -e _main

# Build the main temperature program
$(MAIN_TEMP_TARGET): $(MAIN_TEMP_OBJECTS)
	ld -arch arm64 -o $(MAIN_TEMP_TARGET) $(MAIN_TEMP_OBJECTS) -lSystem -syslibroot `xcrun --show-sdk-path` -e _main

# Pattern rule for assembling source files
%.o: %.s
	as -arch arm64 $< -o $@

# Build both programs
both: $(TARGET) $(TEMP_TARGET)

# Build all programs
all-programs: $(TARGET) $(TEMP_TARGET) $(MAIN_TEMP_TARGET)

# Run the temperature sensor program (default)
run: $(MAIN_TEMP_TARGET)
	@echo "Running temperature sensor with 100ms refresh rate"
	@echo "Press Ctrl+C to stop"
	./$(MAIN_TEMP_TARGET)
	@echo "Exit code: $$?"

# Run the basic program
run-basic: $(TARGET)
	./$(TARGET)
	@echo "Exit code: $$?"

# Run the temperature reader (requires Arduino setup)
run-temp: $(TEMP_TARGET)
	@echo "Make sure Arduino is connected and running the bridge sketch"
	@echo "Update the serial port in src/io/serial.s if needed"
	./$(TEMP_TARGET)
	@echo "Exit code: $$?"

# Run the main temperature program
run-main-temp: $(MAIN_TEMP_TARGET)
	@echo "Running temperature sensor with 100ms refresh rate"
	@echo "Press Ctrl+C to stop"
	./$(MAIN_TEMP_TARGET)
	@echo "Exit code: $$?"

# Clean up generated files
clean:
	rm -f $(BASIC_OBJECTS) $(TEMP_OBJECTS) $(MAIN_TEMP_OBJECTS) $(TARGET) $(TEMP_TARGET) $(MAIN_TEMP_TARGET)

# Rebuild everything
rebuild: clean all

# Show the modular structure
info:
	@echo "Modular Assembly Project Structure:"
	@echo "=================================="
	@echo "Basic Program:"
	@echo "  Main Program:     $(MAIN_SOURCE)"
	@echo "  Math Logic:       $(MATH_SOURCE)"
	@echo "  I/O Operations:   $(IO_SOURCE)"
	@echo "  System Calls:     $(SYSTEM_SOURCE)"
	@echo ""
	@echo "Temperature Reader:"
	@echo "  Main Program:     $(TEMP_MAIN_SOURCE)"
	@echo "  Serial I/O:       $(SERIAL_SOURCE)"
	@echo "  Hardware SPI:     $(HARDWARE_SOURCE)"
	@echo "  + Basic modules above"
	@echo ""
	@echo "Arduino Bridge:     examples/arduino_bridge.ino"
	@echo "Documentation:      docs/MAX31855_INTEGRATION.md"

# Show Arduino setup instructions
arduino-help:
	@echo "Arduino Bridge Setup:"
	@echo "===================="
	@echo "1. Connect MAX31855 to Arduino:"
	@echo "   VCC → 3.3V, GND → GND"
	@echo "   SCK → Pin 13, CS → Pin 10, SO → Pin 12"
	@echo ""
	@echo "2. Upload examples/arduino_bridge.ino to Arduino"
	@echo ""
	@echo "3. Find Arduino serial port:"
	@echo "   ls /dev/tty.usb*"
	@echo ""
	@echo "4. Update device_path in src/io/serial.s"
	@echo ""
	@echo "5. Build and run:"
	@echo "   make temperature_reader"
	@echo "   make run-temp"

.PHONY: all both all-programs run run-basic run-temp run-main-temp clean rebuild info arduino-help