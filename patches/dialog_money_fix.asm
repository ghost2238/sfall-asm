//! NAME dialog_money_fix
/// Fixes the bug where money is not displayed after exiting barter. https://github.com/rotators/Fo1in2/issues/26
//! ASM !//
/// gdialog_bk+0x75
00447ACD | E9 [patch]    | jmp [patch]
[patch]  | 60            | pushad
00000000 | E8 [0x446D30] | call <fallout2.gdProcessUpdate_>
00000000 | 61            | popad
00000000 | E8 [0x4D6DAC] | call <fallout2.win_show_>
00000000 | E9 [0x447AD2] | jmp fallout2.447AD2