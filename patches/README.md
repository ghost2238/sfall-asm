## HintBook

DDRAW => `_GNW95_hDDrawLib` (`0x51e44c`) @ `0x4de8de`

#### NOP variants
| **Length** | **Assembly**                                | **Bytes**                                     |
|:---------- |:------------------------------------------- |:--------------------------------------------- |
| 1 byte     | `nop`                                       | `90`                                          |
| 2 bytes    | `fnop`                                      | `D9 D0`                                       |
| 2 bytes    | `66 nop`                                    | `66 90`                                       |
| 2 bytes    | `mov eax, eax`                              | `8B C0`                                       |
| 3 bytes    | `nop dword ptr [eax]`                       | `0F 1F 00`                                    |
| 3 bytes    | `66 ... nop`                                | `66 66 90`                                    |
| 4 bytes    | `nop dword ptr [eax + 00h]`                 | `0F 1F 40 00`                                 |
| 4 bytes    | `66 ... nop`                                | `66 66 66 90`                                 |
| 5 bytes    | `nop dword ptr [eax + eax*1 + 00h]`         | `0F 1F 44 00 00` sfall's `BlockCall()`        |
| 5 bytes    | `66 ... nop`                                | `66 66 66 66 90`                              |
| 6 bytes    | `66 nop word ptr [eax + eax*1 + 00h]`       | `66 0F 1F 44 00 00`                           |
| 6 bytes    | `66 ... nop`                                | `66 66 66 66 66 90`                           |
| 7 bytes    | `nop dword ptr [eax + 00000000h]`           | `0F 1F 80 00 00 00 00`                        |
| 7 bytes    | `66 ... nop`                                | `66 66 66 66 66 66 90`                        |
| 8 bytes    | `nop dword ptr [eax + eax*1 + 00000000h]`   | `0F 1F 84 00 00 00 00 00`                     |
| 8 bytes    | `66 ... nop`                                | `66 66 66 66 66 66 66 90`                     |
| 9 bytes    | `66 nop word ptr [eax + eax*1 + 00000000h]` | `66 0F 1F 84 00 00 00 00 00`                  |
| 9 bytes    | `66 ... nop`                                | `66 66 66 66 66 66 66 66 90`                  |
| 10 bytes   | `66 ... nop`                                | `66 66 66 66 66 66 66 66 66 90`               |
| 11 bytes   | `66 ... nop`                                | `66 66 66 66 66 66 66 66 66 66 90`            |
| 12 bytes   | `66 ... nop`                                | `66 66 66 66 66 66 66 66 66 66 66 90`         |
| 13 bytes   | `66 ... nop`                                | `66 66 66 66 66 66 66 66 66 66 66 66 90`      |
| 14 bytes   | `66 ... nop`                                | `66 66 66 66 66 66 66 66 66 66 66 66 66 90`   |
| 15 bytes   | `66 ... nop`                                | `66 66 66 66 66 66 66 66 66 66 66 66 66 66 90`|

#### Clearing register
| **Length** | **Assembly**    | **Bytes**        |
|:---------  |:--------------- |:---------------- |
| 5 bytes    | `mov eax, 0`    | `B8 00 00 00 00` |
| **Length** | **Assembly**    | **Bytes**        |
| 2 bytes    | `xor eax, eax`  | `33 C0`          |

#### Setting register
| **Length** | **Assembly**    | **Bytes**        |
|:---------  |:--------------- |:---------------- |
| 5 bytes    | `mov eax, 0x01` | `B8 01 00 00 00` |
| **Length** | **Assembly**    | **Bytes**        |
| 2 bytes    | `xor eax, eax`  | `33 C0`          |
| 1 byte     | `inc eax`       | `40`             |
| **Length** | **Assembly**    | **Bytes**        |
| 2 bytes    | `push 0x01`     | `6A 01`          |
| 1 byte     | `pop eax`       | `58`             |
