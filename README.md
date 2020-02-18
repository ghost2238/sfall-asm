# sfall-asm
Just some code to easily use [asm patches](https://github.com/rotators/Fo1in2/blob/master/Fallout2/Fallout1in2/Mapper/source/scripts/headers/voodoo.h) in [sfall](https://github.com/rotators/sfall) without doing a lot of tedious manual work.

# Patches
| Patch          | Description   | sfall |
| -------------- | ------------- |:--------------- |
| [dialog_fix.asm](patches/dialog_fix.asm) | Bugfix for the dialog button bug | [Accepted](https://github.com/phobos2077/sfall/commit/eb204f0a04f20514b47fafd7e1cbbe7a6270fb3c)  |
| [dialog_money_fix.asm](patches/dialog_money_fix.asm) | Fixes [a bug](https://github.com/rotators/Fo1in2/issues/26) where money is not displayed after exiting barter. | No signal
| [dogmeat_pm_dialog.asm](patches/dogmeat_pm_dialog.asm) | This will replace RoboDog PID with Dogmeat PID in hardcoded list of dogs PIDs | N/A
| [fill_w.asm](patches/fill_w.asm) | [Fill_W that works like in Fallout 1](https://github.com/rotators/Fo1in2/issues/16) | No signal
| [mode_fo1_ending.asm](patches/mode_fo1_ending.asm) | This will disable running the credits after the endgame slides | Accepted
| [no_location_radius.asm](patches/no_location_radius.asm) | No radius when a location is revealed on the worldmap, like in Fallout 1 | [Rejected](https://github.com/phobos2077/sfall/issues/255#issuecomment-516919831) 
| [remove_circle_name.asm](patches/remove_circle_name.asm) | Removes the text under green circles on the worldmap | No signal
| [rest_till_0600.asm](patches/rest_till_0600.asm) | This will change the rest timer "wait until 08:00" to 06:00 like in Fallout 1. | N/A
| [selfrun_disable.asm](patches/selfrun_disable.asm) | Keeps main menu always active | N/A
