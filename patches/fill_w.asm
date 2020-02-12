//! NAME fill_w
/// Fill_W that works like in Fallout 1
// https://github.com/rotators/Fo1in2/issues/16

004C3735 | E9 F576F5FF  | jmp fallout2.41AE2F
0041AE2F | 75 4D        | jne fallout2.41AE7E
00000000 | 83EC 04      | sub esp,4 
00000000 | C60424 00    | mov ss:[esp+4],0 
00000000 | 8B4C24 0C    | mov ecx,ss:[esp+C]
00000000 | 49           | dec ecx 
00000000 | 83F9 03      | cmp ecx,3 
00000000 | 74 39        | je 0x41AE7E
00000000 | 83F9 07      | cmp ecx,7
00000000 | 74 34        | je 0x41AE7E
00000000 | 83F9 0B      | cmp ecx,B                     
00000000 | 74 2F        | je 0x41AE7E                   
00000000 | 83F9 0F      | cmp ecx,F                     
00000000 | 74 2A        | je 0x41AE7E                   
00000000 | 31ED         | xor ebp,ebp                   
00000000 | 894C24 0C    | mov ss:[esp+C],ecx            
00000000 | 6A 02        | push 2                        
00000000 | 8B4424 10    | mov eax,ss:[esp+10]           
00000000 | 89F1         | mov ecx,esi                   
00000000 | 89FB         | mov ebx,edi                   
00000000 | 56           | push esi                      
00000000 | 89EA         | mov edx,ebp                   
00000000 | 45           | inc ebp                       
00000000 | E8 CA850A00  | call 0x4c3434
00000000 | 83FD 07      | cmp ebp,7                     
00000000 | 7C E8        | jl 41AE58                     
00000000 | 8B0424       | mov eax,ss:[esp+4]            
00000000 | 40           | inc eax                       
00000000 | 83F8 02      | cmp eax,2                     
00000000 | 890424       | mov ss:[esp+4],eax            
00000000 | 7C BD        | jl 41AE39           
00000000 | 83C4 04      | add esp,4           
00000000 | 83C4 0C      | add esp,C           
00000000 | E9 B4880A00  | jmp fallout2.4C373A