# SpamLang - the worst programming language ever made by a human being (still better than c++)

## Usage

Install Clang. Path to clang is specified with `--clang-path` flag or `SPAM_CLANG_PATH` env variable. By default `clang`
from path is called.

Compile this miserable source file to an executable:

```
spamlang <source file> -o <output file>
```

### Compiler Flags

- `-o <path>` - output file (optional, default is `out.exe`/`out.o`)
- `-v` / `--verbose` - verbose output
- `-c` / `--compile-only` - compile without linking
- `-g` / `--debug-info` - compile with debug info
- `--clang-path <path>` - path to clang executable (optional, default is `clang`)
- `--emit-asm <path>` - save generated assembly to specified path (optional)

Debug compiler stages:

- `--lexer` - print tokens (spams in logs)
- `--lexer-pretty` - pretty print tokens
- `--parser` - print AST
- `--ir` - print IR
- `--mir` - print MIR
- `--timer` - print every stage time to measure how horrible the performance is

### Environment Variables

- `SPAMLANG_CLANG_PATH` - path to clang executable (optional)

## Features

- [x] Modern syntax optimized for faster parsing
- [x] Named arguments syntax: `spam(a: 123, b: 345)`
- [x] Builtin types: i32
- [x] LSP Server (work in progress)
- [x] Windows ABI (work in progress)
- [ ] System V ABI

## Supported Targets

- [x] x86_64 Windows
- [ ] x86_64 Linux

