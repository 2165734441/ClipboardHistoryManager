@echo off
setlocal
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=false -o ".\dist\ClipboardHistoryManager-win-x64"
endlocal
