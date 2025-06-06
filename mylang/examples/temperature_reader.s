// temperature_reader.s - Example program using MAX31855 via Arduino bridge
.global _main

.text

_main:
    // Open serial connection to Arduino
    bl open_serial
    cmp x0, -1
    beq connection_error
    
    mov x19, x0             // Save file descriptor
    
    // Read temperature from Arduino
    mov x0, x19
    bl read_temperature_from_arduino
    cmp x0, -1
    beq read_error
    
    // Convert temperature to displayable format
    // (x0 contains temperature * 100, e.g., 2550 for 25.50°C)
    mov x20, x0             // Save temperature
    
    // Extract whole degrees
    mov x1, 100
    udiv x0, x20, x1        // x0 = whole degrees
    msub x21, x0, x1, x20   // x21 = remainder (fractional part)
    
    // Convert whole degrees to ASCII and print
    bl convert_to_ascii
    bl store_in_buffer
    bl print_buffer
    
    // Print decimal point
    mov x0, '.'
    bl store_in_buffer
    bl print_buffer
    
    // Convert fractional part to ASCII and print
    mov x0, x21
    mov x1, 10
    udiv x0, x0, x1         // Get tens digit of fractional part
    bl convert_to_ascii
    bl store_in_buffer
    bl print_buffer
    
    // Print units
    mov x0, 'C'
    bl store_in_buffer
    bl print_buffer
    bl print_newline
    
    // Close serial connection
    mov x0, x19
    bl close_serial
    
    // Exit successfully
    bl exit_program

connection_error:
    // Print error message (simplified)
    mov x0, 'E'
    bl convert_to_ascii
    bl store_in_buffer
    bl print_buffer
    bl print_newline
    mov x16, 1              // sys_exit
    mov x0, 1               // error code
    svc 0x80

read_error:
    // Close connection and exit with error
    mov x0, x19
    bl close_serial
    mov x16, 1              // sys_exit
    mov x0, 2               // error code
    svc 0x80