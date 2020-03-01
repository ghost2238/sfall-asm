//! NAME fill_w
/// Fill_W that works like in Fallout 1
/// https://github.com/rotators/Fo1in2/issues/16
/// https://github.com/phobos2077/sfall/issues/287
//! ASM !//

/// fill_w implementation
/// malloc(patch) - this code can be used with --malloc
[patch]  | 75 4D         | jne _ret
00000000 | 83EC 04       | sub esp,4
00000000 | C60424 00     | mov ss:[esp+4],0
/// _loop_begin
00000000 | 8B4C24 0C     | mov ecx,ss:[esp+C]
/// move to the next tile to the left
00000000 | 49            | dec ecx
/// the comparison checks are to see if the tile we are currently on is
/// one of the tiles on the right side of the wm (3, 7, 11 or 15)
/// if it is, it means we've wrapped around
00000000 | 83F9 03       | cmp ecx,3
00000000 | 74 39         | je _end
00000000 | 83F9 07       | cmp ecx,7
00000000 | 74 34         | je _end
00000000 | 83F9 0B       | cmp ecx,B
00000000 | 74 2F         | je _end
00000000 | 83F9 0F       | cmp ecx,F
00000000 | 74 2A         | je _end
00000000 | 31ED          | xor ebp,ebp
00000000 | 894C24 0C     | mov ss:[esp+C],ecx
/// _reveal_subtile
00000000 | 6A 02         | push 2
00000000 | 8B4424 10     | mov eax,ss:[esp+10]
00000000 | 89F1          | mov ecx,esi
00000000 | 89FB          | mov ebx,edi
00000000 | 56            | push esi
00000000 | 89EA          | mov edx,ebp
00000000 | 45            | inc ebp
00000000 | E8 [0x4c3434] | call wmMarkSubTileOffsetVisitedFunc
/// did we uncover all the subtiles in the tile?
/// if not, go to _reveal_subtile and uncover another one
00000000 | 83FD 07       | cmp ebp,7
00000000 | 7C E8         | jl _reveal_subtile
00000000 | 8B0424        | mov eax,ss:[esp+4]
00000000 | 40            | inc eax
00000000 | 83F8 02       | cmp eax,2
00000000 | 890424        | mov ss:[esp+4],eax
00000000 | 7C BD         | jl _loop_begin
/// _end
00000000 | 83C4 04       | add esp,4
/// _ret
00000000 | 83C4 0C       | add esp,C
00000000 | E9 [0x4C373A] | jmp fallout2.4C373A
/// END
//! SSL !//
/// Hook in wmMarkSubTileRadiusVisited_
call VOODOO_MakeJump(0x004C3735, addr)