//! NAME sound_on_wm
/// Button sound when clicking on the worldmap for the ultimate Fallout 1 experience.
//! ASM !//
[[patch]] | 60               | pushad             
00000000  | E8 [0x004CAAA0]  | call 0x004CAAA0
00000000  | 8B15 00005300    | mov edx,ds:[530000] 
00000000  | 83FA 00          | cmp edx,0          
00000000  | 74 1D            | je 410086          
00000000  | 83FA 01          | cmp edx,1          
00000000  | 75 2E            | jne 41009C         
00000000  | 83F8 00          | cmp eax,0          
00000000  | 75 29            | jne 41009C         
00000000  | B8 203E5000      | mov eax,503E20     
00000000  | E8 [0x004519A8]  | call 0x004519A8    
00000000  | C605 00005300 00 | mov ds:[530000],0  
00000000  | EB 16            | jmp 41009C         
00000000  | 83F8 01          | cmp eax,1          
00000000  | 75 11            | jne 41009C         
00000000  | B8 143E5000      | mov eax,503E14     
00000000  | E8 [0x004519A8]  | call 0x004519A8
00000000  | C605 00005300 01 | mov ds:[530000],1  
00000000  | 61               | popad              
00000000  | 01C5             | add ebp,eax        
00000000  | 83EF 16          | sub edi,16         
00000000  | E9 [0x004BFE94]  | jmp 4BFE94
/// END
//! SSL !//
call VOODOO_MakeJump(0x004BFE8F, $addr)