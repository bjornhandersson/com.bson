// output.s - Output operations and buffer management
.data
buffer: .space 16       // Buffer for number conversion
newline: .ascii "\n"

.text

// Function: store_in_buffer
// Purpose: Store ASCII character in buffer
// Input: x0 = ASCII character
// Output: none (modifies buffer)
.global store_in_buffer
store_in_buffer:
    adrp x1, buffer@PAGE
    add x1, x1, buffer@PAGEOFF
    strb w0, [x1]       // Store ASCII character in buffer
    ret                 // Return to caller

// Function: print_buffer
// Purpose: Print the content of buffer to stdout
// Input: none
// Output: none
.global print_buffer
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
.global print_newline
print_newline:
    mov x16, 4          // sys_write system call
    mov x0, 1           // stdout file descriptor
    adrp x1, newline@PAGE
    add x1, x1, newline@PAGEOFF     // newline address
    mov x2, 1           // length (1 character)
    svc 0x80            // make system call
    ret                 // Return to caller