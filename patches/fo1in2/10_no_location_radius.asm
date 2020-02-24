//! NAME location_discover_radius
/// No radius when a location is revealed on the worldmap
/// Modifies sfall code
//! SSL !//
debug("Applying location_discover_radius");
/// Calculate where ddraw.sfall::wmAreaMarkVisitedState_hack+0x51 and write 0 to it.
call VOODOO_SafeWrite8(VOODOO_GetHookFuncOffset(0x4C4670, 0x51), 0x0);
debug("Done.");