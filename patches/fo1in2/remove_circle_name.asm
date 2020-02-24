//! NAME remove_circle_name
/// Removes the text under green circles on the worldmap
/// Used by Classic Worldmap mod

//! SSL !//
/// fallout2.wmInterfaceDrawCircleOverlay+0xD2
call VOODOO_WriteNop(0x4c407a);
/// fallout2.wmInterfaceDrawCircleOverlay+0xEC
call VOODOO_BlockCall(0x4c4094,6)
