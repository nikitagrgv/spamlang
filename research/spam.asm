    .intel_syntax noprefix

    .data
msg:
    .asciz "hello, world!\n"

    .text
    .globl main
main:
    push rbp
    mov rbp, rsp
	
	# align + shadow space
	sub rsp, 0x00 + 0x20
	
	lea rcx, [rip + msg]
	call count
	
	#lea rcx, [rip + msg]
	#mov rdx, 14
	#call print

    mov rsp, rbp
    pop rbp
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
	push rcx
	mov rax, rcx
	dec rax
count_iterate:
	inc rax
	cmp byte ptr [rax], 0
	jne count_iterate
count_end:
	pop rcx
	sub rax, rcx
	ret