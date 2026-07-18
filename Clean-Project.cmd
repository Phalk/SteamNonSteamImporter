@echo off
setlocal
cd /d "%~dp0"
for %%D in (bin obj .vs packages) do (
    if exist "%%D" rmdir /s /q "%%D"
)
del /q *.user *.suo *.pext 2>nul
echo Generated files removed. Restore NuGet packages before the next build.
