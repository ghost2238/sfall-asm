//! NAME selfrun_disable
/// Keeps main menu always active

//! ASM !//

/// prevent playing intro movie and selfruns
00480BFB | 0F1F4400 00 | nop dword ptr ds:[eax+eax],eax

/// ignore CTRL+R on main screen
//  also a cool place for own on-demand call
00480C90 | 6666:90     | nop
00000000 | 33FF        | xor edi,edi
