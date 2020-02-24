//! NAME CalcHook
// calculate the destination of a hook/jump + offset
[patch]  | 2E:8B30 | mov esi,cs:[eax]
00000000 | 01D0    | add eax,edx
00000000 | 01F0    | add eax,esi
00000000 | 83C0 04 | add eax,4
00000000 | C3      | ret
