//! NAME sound_on_wm
/// Sound when clicking to move on WM, when clicking the player location/hotspot button and inside the town interface.
/// See https://www.youtube.com/watch?v=dGZjse7uC_0&t=50m for reference on how it works in the original.
//! ASM !//
/// Sound when clicking location/hotspot
[[patch]] | 60               | pushad
000000000 | B8 343E5000      | mov eax,<fallout2.Ib2p1xx1_1>
000000000 | E8 [0x004519A8]  | call ds:[<int playSound>]
000000000 | C605 00005300 01 | mov ds:[<bool pressed>],1
000000000 | 61               | popad
000000000 | 3E:A1 902E6700   | mov eax,ds:[<hotspot2_pic>]  
000000000 | E9 [0x4C425C]    | jmp ds:[<int afterHotspot>]
/// Sound when releasing mouse while clicking location/hotspot
000000000 | 60               | pushad
000000000 | A0 00005300      | mov al, ds:[<bool pressed>]
000000000 | 3C 01            | cmp al,1
000000000 | 75 11            | jne <exit>
000000000 | B8 403E5000      | mov eax,<fallout2.Ib2lu1x1_1>
000000000 | E8 [0x004519A8]  | call ds:[<int playSound>]
000000000 | C605 00005300 00 | mov ds:[<bool pressed>],0
/* exit: */
000000000 | 61               | popad                        
000000000 | 3E:A1 882E6700   | mov eax,ds:[<hotspot1_pic>]
000000000 | E9 [0x4C425C]    | jmp ds:[<int afterHotspot>]
/// wmPartyInitWalking_ - sound when clicking on the WM to walk.
000000000 | 60               | pushad
000000000 | B8 143E5000      | mov eax,<fallout2.Ib1p1xx1_7>
000000000 | E8 [0x004519A8]  | call <fallout2.gsound_play_sfx_file_>
000000000 | 61               | popad
000000000 | E8 [0x004C1E54]  | call <fallout2.wmPartyInitWalking_>
000000000 | E9 [0x004C02DF]  | jmp fallout2.4C02DF
/// Register sound for townmap buttons
000000000 | 8987 D82D6700    | mov ds:[edi+<_wmTownMapButtonId>],eax
000000000 | 50               | push eax
000000000 | BB 90194500      | mov ebx,<fallout2.gsound_med_butt_rele
000000000 | BA 88194500      | mov edx,<fallout2.gsound_med_butt_pres
000000000 | E8 [0x004D87F8]  | call <fallout2.win_register_button_sou
000000000 | 58               | pop eax
000000000 | E9 [0x004C4B9A]  | jmp fallout2.4C4B9A

//! SSL !//
call VOODOO_MakeJump(0x4C4257, $addr)
call VOODOO_MakeJump(0x4C4250, $addr+30)
call VOODOO_MakeJump(0x4C02DA, $addr+69)
call VOODOO_MakeJump(0x4C4B94, $addr+91)
write_byte (0x4C4B99, 0x90)
/// END