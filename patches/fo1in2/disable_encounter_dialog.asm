//! NAME disable_encounter_dialog
/// Disables the "Encounter! Investigate?" dialog
//! ASM !//
004C0B9D | B0 01 | mov al,1
//! SSL !//
call VOODOO_WriteNop(0x4C0B75, 40, true)