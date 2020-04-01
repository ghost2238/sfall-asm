#!/bin/sh

set -e

dir=bin

if [ ! -d $dir/Microsoft.Net.Compilers.Toolset ]; then
   mkdir -p $dir
   if [ ! -f $dir/nuget.exe ]; then
      curl "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -o "$dir/nuget.exe"
   fi
   $dir/nuget.exe install Microsoft.Net.Compilers.Toolset -ExcludeVersion -OutputDirectory $dir
fi

$(find $dir -iname csc.exe) -debug:embedded -out:sfall-asm.exe -recurse:*.cs -preferreduilang:en
