## HintBook

#### NOP variants
| **Length** | **Assembly**                                | **Bytes**                    |
|:---------- |:------------------------------------------- |:---------------------------- |
| 1 byte     | `nop`                                       | `90`                         |
| 2 bytes    | `fnop`                                      | `D9 D0`                      |
| 2 bytes    | `66 nop`                                    | `66 90`                      |
| 3 bytes    | `nop dword ptr [eax]`                       | `0F 1F 00`                   |
| 4 bytes    | `nop dword ptr [eax + 00h]`                 | `0F 1F 40 00`                |
| 5 bytes    | `nop dword ptr [eax + eax*1 + 00h]`         | `0F 1F 44 00 00` (BlockCall) |
| 6 bytes    | `66 nop word ptr [eax + eax*1 + 00h]`       | `66 0F 1F 44 00 00`          |
| 7 bytes    | `nop dword ptr [eax + 00000000h]`           | `0F 1F 80 00 00 00 00`       |
| 8 bytes    | `nop dword ptr [eax + eax*1 + 00000000h]`   | `0F 1F 84 00 00 00 00 00`    |
| 9 bytes    | `66 nop word ptr [eax + eax*1 + 00000000h]` | `66 0F 1F 84 00 00 00 00 00` |

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
