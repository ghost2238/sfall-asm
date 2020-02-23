// SafeMemSet8(void* ptr, uint8 value, int num) using the Watcom calling convention.
//! NAME SafeMemSet

//! ASM !//
[patch]    | 52               | push edx   - int num (bytes)
00000000   | 53               | push ebx   - uint8 value
00000000   | 50               | push eax   - void* ptr
00000000   | 57               | push edi
00000000   | 83EC 04          | sub esp,4  - DWORD oldProtect;
00000000   | 54               | push esp   - &oldProtect
00000000   | 6A 40            | push 40    - PAGE_EXECUTE_READWRITE
00000000   | 53               | push ebx   - int num (bytes)
00000000   | 50               | push eax   - void* ptr
00000000   | 2EFF15 18026C00  | call cs:[<&fallout2.VirtualProtect>]
00000000   | 83C4 04          | add esp,4
00000000   | 8B4C24 08        | mov ecx,ss:[esp+0x8] - int num (bytes)
00000000   | 8A4424 0C        | mov al, ss:[esp+0xC] - uint8 value
00000000   | 8B7C24 04        | mov edi,ss:[esp+0x4] - void* ptr
00000000   | F3AA             | rep stosb
00000000   | 5F               | pop edi
00000000   | 58               | pop eax
00000000   | 5B               | pop ebx
00000000   | 5A               | pop edx
00000000   | C3               | ret