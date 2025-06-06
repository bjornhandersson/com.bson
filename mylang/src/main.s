// main.s - Main program entry point
.global _main

.text

_main:
    // Calculate result using function
    mov x0, 5           // First number
    mov x1, 3           // Second number
    bl add_numbers      // Call function (result in x0)
    
    // Convert to ASCII using function
    bl convert_to_ascii // Call function (ASCII char in x0)
    
    // Store in buffer using function
    bl store_in_buffer  // Call function
    
    // Print the digit using function
    bl print_buffer     // Call function
    
    // Print newline using function
    bl print_newline    // Call function
    
    // Exit using function
    bl exit_program     // Call function