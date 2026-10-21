#!/bin/sh
# Builds akaneiro_redirect.so for 32-bit (the game's Akaneiro.x86) and 64-bit into linux/redirect/lib32, lib64.
# Uses gcc/clang when they can build 32-bit, otherwise zig (pip install ziglang, or zig in PATH).
set -e
cd "$(dirname "$0")"
SRC=akaneiro_redirect.c
mkdir -p lib32 lib64

if command -v zig >/dev/null 2>&1; then
    ZIG="zig"
elif [ -x ../../../.toolchain/bin/python ] && ../../../.toolchain/bin/python -m ziglang version >/dev/null 2>&1; then
    ZIG="../../../.toolchain/bin/python -m ziglang"
elif python3 -m ziglang version >/dev/null 2>&1; then
    ZIG="python3 -m ziglang"
fi

if [ -n "$ZIG" ]; then
    # Old glibc targets, so the library loads with any glibc the game may run against.
    $ZIG cc -target x86-linux-gnu.2.17 -shared -fPIC -O2 -o lib32/akaneiro_redirect.so $SRC -ldl
    $ZIG cc -target x86_64-linux-gnu.2.17 -shared -fPIC -O2 -o lib64/akaneiro_redirect.so $SRC -ldl
else
    CC=${CC:-cc}
    $CC -m32 -shared -fPIC -O2 -o lib32/akaneiro_redirect.so $SRC -ldl
    $CC -m64 -shared -fPIC -O2 -o lib64/akaneiro_redirect.so $SRC -ldl
fi
echo "Built lib32/akaneiro_redirect.so and lib64/akaneiro_redirect.so"
