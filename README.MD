# SpamLang - the worst programming language ever made by a human being (still better than c++)

## Usage

Install Clang. Path to clang is specified with `--clang-path` flag or `SPAM_CLANG_PATH` env variable. By default `clang`
from path is called.

Compile this miserable source file to an executable:

```
spamlang <source file> -o <output file>
```

### Compiler Flags

- `-o <path>` - output file
- `-v` / `--verbose` - verbose output
- `-c` / `--compile-only` - compile without linking
- `--clang-path <path>` - path to clang executable (optional)

Debug compiler stages:

- `--lexer` - print tokens (spams in logs)
- `--lexer-pretty` - pretty print tokens
- `--parser` - print AST
- `--ir` - print IR
- `--mir` - print MIR
- `--timer` - print every stage time to measure how horrible the performance is

### Environment Variables

- `SPAM_CLANG_PATH` - path to clang executable (optional)

## Features

- [x] Syntax is better (for compiling and for mental health) than C++
- [x] Builtin types: i32
- [x] Windows ABI
- [ ] System V ABI

## Supported Targets
- [x] x86_64 Windows
- [ ] x86_64 Linux

