//! NAME dialog_money_fix
/// Fixes the bug where money is not displayed after exiting barter. https://github.com/rotators/Fo1in2/issues/26
//! ASM !//
/// gdialog_bk+0x75
00447ACD | E9 B433FDFF | jmp fallout2.41AE86
0041AE86 | 60          | pushad
00000000 | E8 A4BE0200 | call <fallout2.gdProcessUpdate_>
00000000 | 61          | popad
00000000 | E8 1ABF0B00 | call <fallout2.win_show_>
00000000 | E9 3BCC0200 | jmp fallout2.447AD2