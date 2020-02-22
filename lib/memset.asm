//! NAME SafeMemSet8
//! BODY !//
//! ASM !//
[address] | 57               | push edi           
00000000  | 51               | push ecx           
00000000  | 50               | push eax
00000000  | 83EC 04          | sub esp,4  - DWORD oldProtect;
00000000  | 54               | push esp   - &oldProtect
00000000  | 6A 40            | push 40    - PAGE_EXECUTE_READWRITE
00000000  | FF7424 18        | push ss:[esp+18] - int num (bytes)
00000000  | FF7424 10        | push ss:[esp+10] - void* ptr
00000000  | 2EFF15 18026C00  | call cs:[<&fallout2.VirtualProtect>]
00000000  | 83C4 04          | add esp,4
00000000  | 8B4C24 18        | mov ecx,ss:[esp+0x18] - int num (bytes)
00000000  | 8A4424 14        | mov al,ss:[esp+0x14]  - uint8 value
00000000  | 8B7C24 10        | mov edi,ss:[esp+0x10] - void* ptr
00000000  | F3AA             | rep stosb
00000000  | 5F               | pop edi
00000000  | 59               | pop ecx
00000000  | 58               | pop eax
00000000  | C3               | ret