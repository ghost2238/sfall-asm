//! NAME no_location_radius
/// No radius when a location is revealed on the worldmap
/// Modifies sfall code

//! ASM !//
/// _defam
0041AE05 | 60               | pushad
0041AE06 | 2EA1 70464C00    | mov eax,cs:[4C4670]
/// Calculate where ddraw.sfall::wmAreaMarkVisitedState_hack+0x51 is
0041AE0C | BA C5464C00      | mov edx,fallout2.4C46C5
0041AE11 | 01D0             | add eax,edx
0041AE13 | 89C6             | mov esi,eax
/// esi is now the address where the radius byte is
/// since the memory of the code address is memory protected, 
/// we need to use VirtualProtect to change this
0041AE15 | 83EC 04          | sub esp,4
0041AE18 | 54               | push esp
0041AE19 | 6A 40            | push 40
0041AE1B | 6A 01            | push 1
0041AE1D | 50               | push eax
/// Requires r_fall
0041AE1E | 2EFF15 04004100  | call cs:[<&VirtualProtect>]
0041AE25 | C606 00          | mov ds:[esi],0
0041AE28 | 83C4 04          | add esp,4
0041AE2B | 61               | popad
0041AE2C | C3               | ret