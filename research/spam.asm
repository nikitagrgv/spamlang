    .intel_syntax noprefix

########################################
	.section .rdata,"dr"
msg:
    .asciz "hello, world!\n"

########################################
	.bss
	.balign 8
cached_out_handle:
	.zero 4

########################################
    .text
    .globl main
main:
	push rbx

    push rbp
    mov rbp, rsp
	
	# align + locals + shadow space
	sub rsp, 0x08 + 0x08 + 0x20
	
	lea rcx, [rip + msg]
	call count

	# save count in locals
	mov dword ptr [rbp - 0x08], eax

	mov rbx, 5
.Lmain_loop:
	lea rcx, [rip + msg]
	mov edx, dword ptr [rbp - 0x08]
	call print
	dec rbx
	jnz .Lmain_loop

    mov rsp, rbp
    pop rbp
	pop rbx
    ret

# print(str: ptr, size: i32) -> i32
print:
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
	#mov qword ptr [rbp-0x10], 0 # don't do like that
	mov qword ptr [rsp+0x20], 0
	call WriteConsoleA

	# return num bytes written
	mov eax, [rbp+0x20]

	mov rsp, rbp
	pop rbp
	ret

# count(str: ptr) -> i32
count:
	lea rax, [rcx - 1]
.Lcount_iterate:
	inc rax
	cmp byte ptr [rax], 0
	jne .Lcount_iterate
	sub rax, rcx
	ret