//! NAME codecave_selfrun

//! ASM !//
/// ignore CTRL+R on main screen
00480C90 | 6666:90      | nop
00000000 | 33FF         | xor edi,edi
/// ignore main_selfrun_exit_ on closing game
00480CA2 | 66666666:90  | nop

//! SSL !//
/// clear main_selfrun_init_, main_selfrun_exit_, main_selfrun_record_
/// skips area occupied by SafeMemSet
VOODOO_CAVE(0x480f0d, 397);
