// Fixes the bug that causes the barter button to not animate until after leaving the trade screen. 
// The bug is due to the pointers to the frm graphics not being loaded, causing the button the get default graphics (and no button_down graphic), so we insert a hook in gdialog_window_create_ before the button is added to the window. In this hook function we load the graphic.
0044A785 | E9 FE06FDFF | jmp fallout2.41AE88
0041AE88 | 6A 00       | push 0                                 
00000000 | BA 60000000 | mov edx,60                             
00000000 | B8 06000000 | mov eax,6                              
00000000 | 31C9        | xor ecx,ecx                            
00000000 | 31DB        | xor ebx,ebx                            
00000000 | E8 EBEDFFFF | call <fallout2.art_id_>                
00000000 | B9 6CF45800 | mov ecx,fallout2.58F46C                
00000000 | 31DB        | xor ebx,ebx                            
00000000 | 31D2        | xor edx,edx                            
00000000 | E8 DDE2FFFF | call <fallout2.art_ptr_lock_data_>     
00000000 | A3 ACF45800 | mov ds:[_dialog_red_button_up_buf],eax                    
00000000 | 85C0        | test eax,eax                           
00000000 | 74 2A       | je fallout2.41AEDE
00000000 | 6A 00       | push 0                                 
00000000 | BA 5F000000 | mov edx,5F                             
00000000 | B8 06000000 | mov eax,6                              
00000000 | 31C9        | xor ecx,ecx                            
00000000 | 31DB        | xor ebx,ebx                            
00000000 | E8 BFEDFFFF | call <fallout2.art_id_>                
00000000 | B9 BCF45800 | mov ecx,fallout2.58F4BC                
00000000 | 31DB        | xor ebx,ebx                            
00000000 | 31D2        | xor edx,edx                            
00000000 | E8 B1E2FFFF | call <fallout2.art_ptr_lock_data_>     
00000000 | A3 A4F45800 | mov ds:[_dialog_red_button_down_buf],eax                    
00000000 | 89C5        | mov ebp,eax                            
00000000 | E9 A8F80200 | jmp fallout2.44A78B