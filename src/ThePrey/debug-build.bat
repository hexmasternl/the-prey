@echo off
setlocal

npx ionic build --prod
if %errorlevel% neq 0 exit /b %errorlevel%

npx cap sync android
if %errorlevel% neq 0 exit /b %errorlevel%

cd .\android\
if %errorlevel% neq 0 exit /b %errorlevel%

.\gradlew assembleDebug
if %errorlevel% neq 0 exit /b %errorlevel%

cd ..

