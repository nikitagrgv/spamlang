    .intel_syntax noprefix

    .data
msg:
    .asciz "hello, world!\n"

    .text
    .globl main
main:
    push rbp
    mov rbp, rsp

    # locals
    sub rsp, 0x08
    # arguments
    sub rsp, 0x08
    # align + shadow space
    sub rsp, 0x00 + 0x20

    # GetStdHandle
    mov ecx, -11
    call GetStdHandle

    # WriteConsoleA
    mov rcx, rax
    lea rdx, [rip + msg]
    mov r8d, 14
    lea r9, [rbp-0x08]
    mov qword ptr [rbp-0x10], 0
    call WriteConsoleA

    mov eax, [rbp-0x08]

    mov rsp, rbp
    pop rbp
    ret

# myprint(str: ptr, size: i32) -> i32
myprint:
	push rbp
	mov rbp, rsp
	
	# align + call's args + shadow space for calls 
	sub rsp, 0x08 + 0x08 + 0x20

	# save args in OUR shadow space (shadow space: rbp+0x10...rbp+0x30)
	mov qword ptr [rbp+0x10], rcx
	mov dword ptr [rbp+0x18], edx

	mov ecx, -11
	call GetStdHandle

	mov rcx, rax
	mov rdx, [rbp+0x10]
	mov r8d, [rbp+0x18]
	lea r9, [rbp+0x20]
	mov qword ptr [rbp-0x08]
	call WriteConsoleA

	# return num bytes written
	mov eax, [rbp+0x20]

	mov rsp, rbp
	pop rbp
	ret