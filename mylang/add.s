.global _main

.data
buffer: .space 16       // Buffer for number conversion
newline: .ascii "\n"

.text

// Function: add_numbers
// Purpose: Add two numbers together
// Input: x0 = first number, x1 = second number
// Output: x0 = result
add_numbers:
    add x0, x0, x1      // Add x1 to x0
    ret                 // Return to caller

// Function: convert_to_ascii
// Purpose: Convert single digit number to ASCII character
// Input: x0 = number (0-9)
// Output: x0 = ASCII character
convert_to_ascii:
    add x0, x0, '0'     // Convert to ASCII ('0' = 48)
    ret                 // Return to caller

// Function: store_in_buffer
// Purpose: Store ASCII character in buffer
// Input: x0 = ASCII character
// Output: none (modifies buffer)
store_in_buffer:
    adrp x1, buffer@PAGE
    add x1, x1, buffer@PAGEOFF
    strb w0, [x1]       // Store ASCII character in buffer
    ret                 // Return to caller

// Function: print_buffer
// Purpose: Print the content of buffer to stdout
// Input: none
// Output: none
print_buffer:
    mov x16, 4          // sys_write system call
    mov x0, 1           // stdout file descriptor
    adrp x1, buffer@PAGE
    add x1, x1, buffer@PAGEOFF      // buffer address
    mov x2, 1           // length (1 character)
    svc 0x80            // make system call
    ret                 // Return to caller

// Function: print_newline
// Purpose: Print a newline character to stdout
// Input: none
// Output: none
print_newline:
    mov x16, 4          // sys_write system call
    mov x0, 1           // stdout file descriptor
    adrp x1, newline@PAGE
    add x1, x1, newline@PAGEOFF     // newline address
    mov x2, 1           // length (1 character)
    svc 0x80            // make system call
    ret                 // Return to caller

// Function: exit_program
// Purpose: Exit the program with success code
// Input: none
// Output: none (program terminates)
exit_program:
    mov x16, 1          // sys_exit system call
    mov x0, 0           // exit code (0 = success)
    svc 0x80            // make system call
    // No ret needed - program terminates

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