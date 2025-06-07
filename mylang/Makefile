# Makefile for ARM64 assembly temperature sensor
TARGET = main_temperature

# Source files
MAIN_SOURCE = src/main.s
MATH_SOURCE = math.s
IO_SOURCE = src/io/output.s
SYSTEM_SOURCE = src/system/exit.s
SERIAL_SOURCE = src/io/serial.s
HARDWARE_SOURCE = src/hardware/spi.s

# Object files
SOURCES = $(MAIN_SOURCE) $(MATH_SOURCE) $(IO_SOURCE) $(SYSTEM_SOURCE) $(SERIAL_SOURCE) $(HARDWARE_SOURCE)
OBJECTS = $(SOURCES:.s=.o)

# Default target
all: $(TARGET)

# Build the temperature sensor program
$(TARGET): $(OBJECTS)
	ld -arch arm64 -o $(TARGET) $(OBJECTS) -lSystem -syslibroot `xcrun --show-sdk-path` -e _main

# Pattern rule for assembling source files
%.o: %.s
	as -arch arm64 $< -o $@

# Run the temperature sensor
run: $(TARGET)
	./$(TARGET)

# Clean up generated files
clean:
	rm -f $(OBJECTS) $(TARGET)

# Rebuild everything
rebuild: clean all

.PHONY: all run clean rebuild