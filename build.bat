@echo off
set "csc=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
%csc% /out:SuperClicker.exe /optimize /platform:x64 /target:exe clicker.cs
echo Сборка завершена!
pause