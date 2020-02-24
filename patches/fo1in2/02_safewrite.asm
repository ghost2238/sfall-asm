// We don't care about changing protection back from 
// PAGE_EXECUTE_READWRITE since this should be used for writing/modifying code.
//! NAME SafeWrite
/// SafeWrite8
[patch]    | 52               | push edx   - int8 value
00000000   | 50               | push eax   - void* ptr
00000000   | 83EC 04          | sub esp,4  - DWORD oldProtect;
00000000   | 54               | push esp   - &oldProtect
00000000   | 6A 40            | push 40    - PAGE_EXECUTE_READWRITE
00000000   | 6A 01            | push 1     - byte
00000000   | 50               | push eax   - void* ptr
00000000   | 2EFF15 18026C00  | call cs:[<&fallout2.VirtualProtect>]
00000000   | 83C4 04          | add esp,4
00000000   | 8B4424 04        | mov eax,ss:[esp+4]
00000000   | 8B5424 00        | mov edx,ss:[esp]
00000000   | 8802             | mov ds:[edx],al
00000000   | 58               | pop eax
00000000   | 5A               | pop edx
00000000   | C3               | ret
/// SafeWrite16
00000000   | 52               | push edx   - int16 value
00000000   | 50               | push eax   - void* ptr
00000000   | 83EC 04          | sub esp,4  - DWORD oldProtect;
00000000   | 54               | push esp   - &oldProtect
00000000   | 6A 40            | push 40    - PAGE_EXECUTE_READWRITE
00000000   | 6A 02            | push 2     - 2 bytes
00000000   | 50               | push eax   - void* ptr
00000000   | 2EFF15 18026C00  | call cs:[<&fallout2.VirtualProtect>]
00000000   | 83C4 04          | add esp,4
00000000   | 8B4424 04        | mov eax,ss:[esp+4]
00000000   | 8B5424 00        | mov edx,ss:[esp]
00000000   | 66:8902          | mov ds:[edx],ax
00000000   | 58               | pop eax
00000000   | 5A               | pop edx
00000000   | C3               | ret
/// SafeWrite32
00000000   | 52               | push edx   - int32 value
00000000   | 50               | push eax   - void* ptr
00000000   | 83EC 04          | sub esp,4  - DWORD oldProtect;
00000000   | 54               | push esp   - &oldProtect
00000000   | 6A 40            | push 40    - PAGE_EXECUTE_READWRITE
00000000   | 6A 04            | push 4     - 4 bytes
00000000   | 50               | push eax   - void* ptr
00000000   | 2EFF15 18026C00  | call cs:[<&fallout2.VirtualProtect>]
00000000   | 83C4 04          | add esp,4
00000000   | 8B4424 04        | mov eax,ss:[esp+4]
00000000   | 8B5424 00        | mov edx,ss:[esp]
00000000   | 8902             | mov ds:[edx],eax
00000000   | 58               | pop eax
00000000   | 5A               | pop edx
00000000   | C3               | ret
//! ASM !//