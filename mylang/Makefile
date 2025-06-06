# Makefile for ARM64 assembly program with modular structure
TARGET = add
MAIN_SOURCE = src/main.s
MATH_SOURCE = math.s
IO_SOURCE = src/io/output.s
SYSTEM_SOURCE = src/system/exit.s

SOURCES = $(MAIN_SOURCE) $(MATH_SOURCE) $(IO_SOURCE) $(SYSTEM_SOURCE)
OBJECTS = $(SOURCES:.s=.o)

# Default target
all: $(TARGET)

# Build the executable
$(TARGET): $(OBJECTS)
	ld -arch arm64 -o $(TARGET) $(OBJECTS) -lSystem -syslibroot `xcrun --show-sdk-path` -e _main

# Pattern rule for assembling source files
%.o: %.s
	as -arch arm64 $< -o $@

# Run the program and show exit code
run: $(TARGET)
	./$(TARGET)
	@echo "Exit code: $$?"

# Clean up generated files
clean:
	rm -f $(OBJECTS) $(TARGET)

# Rebuild everything
rebuild: clean all

# Show the modular structure
info:
	@echo "Modular Assembly Project Structure:"
	@echo "=================================="
	@echo "Main Program:     $(MAIN_SOURCE)"
	@echo "Math Logic:       $(MATH_SOURCE)"
	@echo "I/O Operations:   $(IO_SOURCE)"
	@echo "System Calls:     $(SYSTEM_SOURCE)"
	@echo ""
	@echo "Generated Objects: $(OBJECTS)"
	@echo "Target Executable: $(TARGET)"

.PHONY: all run clean rebuild info