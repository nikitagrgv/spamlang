using System.Diagnostics;

namespace Compiler;

public enum Reg
{
    Rax,
    Rcx,
    Rdx,
    Rbx,
    Rsp,
    Rbp,
    Rsi,
    Rdi,

    R8,
    R9,
    R10,
    R11,
    R12,
    R13,
    R14,
    R15,
}

public enum RegClass
{
    Int,
    Float,
}

public static class Regs
{
    private static readonly string[][] Names =
    [
        ["rax", "eax", "ax", "al"],
        ["rcx", "ecx", "cx", "cl"],
        ["rdx", "edx", "dx", "dl"],
        ["rbx", "ebx", "bx", "bl"],
        ["rsp", "esp", "sp", "spl"],
        ["rbp", "ebp", "bp", "bpl"],
        ["rsi", "esi", "si", "sil"],
        ["rdi", "edi", "di", "dil"],
        ["r8", "r8d", "r8w", "r8b"],
        ["r9", "r9d", "r9w", "r9b"],
        ["r10", "r10d", "r10w", "r10b"],
        ["r11", "r11d", "r11w", "r11b"],
        ["r12", "r12d", "r12w", "r12b"],
        ["r13", "r13d", "r13w", "r13b"],
        ["r14", "r14d", "r14w", "r14b"],
        ["r15", "r15d", "r15w", "r15b"],
    ];

    public static string GetName(this Reg reg, int size)
    {
        int index = size switch
        {
            8 => 0,
            4 => 1,
            2 => 2,
            1 => 3,
            _ => throw new UnreachableException()
        };

        string name = Names[(int)reg][index];
        return name;
    }

    public static string MemPrefix(int size)
    {
        return size switch
        {
            8 => "qword ptr",
            4 => "dword ptr",
            2 => "word ptr",
            1 => "byte ptr",
            _ => throw new UnreachableException()
        };
    }
}