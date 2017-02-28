@echo off
start /d "%~dp0RelayServerListener\RelayServerListener\bin\Debug" RelayServerListener.exe
timeout /t 2 /nobreak
start /d "%~dp0RelayClientSender\RelayClientSender\bin\Debug" RelayClientSender.exe
