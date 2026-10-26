@echo off
rem Runs Claude Code unattended on Opus 5.5 to implement all open OpenSpec changes.
rem The prompt lives in auto\instructions.md; progress is logged to auto\log.md.
setlocal

rem Run from the repository root, wherever this script is started from.
pushd "%~dp0.."

where claude >nul 2>&1 || (
  echo Claude Code CLI not found on PATH. Install it first: https://claude.com/claude-code
  popd
  exit /b 1
)

if not exist "auto\instructions.md" (
  echo auto\instructions.md not found.
  popd
  exit /b 1
)

claude --model claude-opus-5-5 --permission-mode auto --name "auto-openspec" "Read auto/instructions.md and follow it exactly, from start to finish, without asking questions."

set EXITCODE=%ERRORLEVEL%
popd
endlocal & exit /b %EXITCODE%
