@echo off
title Rastreador de Maos - DitadoEstrelado (MediaPipe)
echo Iniciando o rastreador de maos (MediaPipe)...
echo Deixe esta janela aberta enquanto joga!
echo.
cd /d "%~dp0RastreadorPython"
py -u rastreador_maos.py
echo.
echo O rastreador foi encerrado.
pause
