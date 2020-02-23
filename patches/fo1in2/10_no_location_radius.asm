//! NAME location_discover_radius
/// No radius when a location is revealed on the worldmap
/// Modifies sfall code

//! BODY !//
//! SSL !//
debug("Writing location_discover_radius patch to [patch].");
//! ASM !//
[patch]  | 60               | pushad
00000000 | 2EA1 70464C00    | mov eax,cs:[4C4670]
/// Calculate where ddraw.sfall::wmAreaMarkVisitedState_hack+0x51 is
00000000 | BA C5464C00      | mov edx,fallout2.4C46C5
00000000 | 01D0             | add eax,edx
00000000 | 89C6             | mov esi,eax
/// esi is now the address where the radius byte is
/// since the memory of the code address is read-only protected,
/// we need to use VirtualProtect to change this
00000000 | 83EC 04          | sub esp,4
00000000 | 54               | push esp
00000000 | 6A 40            | push 40
00000000 | 6A 01            | push 1
00000000 | 50               | push eax
00000000 | 2EFF15 18026C00  | call cs:[<&fallout2.VirtualProtect>]
00000000 | C606 00          | mov ds:[esi],0
00000000 | 83C4 04          | add esp,4
00000000 | 61               | popad
00000000 | C3               | ret
//! SSL !//
call_offset_v0([patch]);
debug("Done.");