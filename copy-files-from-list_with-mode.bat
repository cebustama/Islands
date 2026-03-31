@echo off
setlocal
cd /d "%~dp0"

set "ROOT=%~1"
if "%ROOT%"=="" set "ROOT=%~dp0"

set "LIST_FILE=%~dp0files.txt"
set "ZIP_FILE=%~dp0file_copies.zip"
set "PS1_FILE=%TEMP%\copy_files_to_zip_%RANDOM%_%RANDOM%.ps1"

if not exist "%LIST_FILE%" (
    echo ERROR: "%LIST_FILE%" was not found.
    pause
    exit /b 1
)

echo.
echo Choose zip structure:
echo   1^)^ Keep folder structure from files.txt inside the zip
echo   2^)^ Put all files loose at the zip root ^(no folders^)
echo.
choice /c 12 /n /m "Select 1 or 2: "
if errorlevel 2 (
    set "ZIP_MODE=flat"
) else (
    set "ZIP_MODE=paths"
)

echo.
echo Mode selected: %ZIP_MODE%
echo.

set "ROOT_ENV=%ROOT%"
set "LIST_FILE_ENV=%LIST_FILE%"
set "ZIP_FILE_ENV=%ZIP_FILE%"
set "ZIP_MODE_ENV=%ZIP_MODE%"

> "%PS1_FILE%" echo $ErrorActionPreference = 'Stop'
>> "%PS1_FILE%" echo $rootResolved = (Resolve-Path -LiteralPath $env:ROOT_ENV).Path
>> "%PS1_FILE%" echo $rootNormalized = [System.IO.Path]::GetFullPath($rootResolved)
>> "%PS1_FILE%" echo $separator = [System.IO.Path]::DirectorySeparatorChar.ToString()
>> "%PS1_FILE%" echo $rootPrefix = $rootNormalized
>> "%PS1_FILE%" echo if (-not $rootPrefix.EndsWith($separator)) { $rootPrefix += $separator }
>> "%PS1_FILE%" echo $listFile = (Resolve-Path -LiteralPath $env:LIST_FILE_ENV).Path
>> "%PS1_FILE%" echo $zipFile = [System.IO.Path]::GetFullPath($env:ZIP_FILE_ENV)
>> "%PS1_FILE%" echo $zipMode = $env:ZIP_MODE_ENV
>> "%PS1_FILE%" echo $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('file_copies_' + [System.Guid]::NewGuid().ToString('N'))
>> "%PS1_FILE%" echo [void](New-Item -ItemType Directory -Path $tempDir)
>> "%PS1_FILE%" echo $copiedCount = 0
>> "%PS1_FILE%" echo $seenDestinations = @{}
>> "%PS1_FILE%" echo function Get-UniqueFlatName([string]$directory, [string]$fileName) {
>> "%PS1_FILE%" echo     $baseName = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
>> "%PS1_FILE%" echo     $extension = [System.IO.Path]::GetExtension($fileName)
>> "%PS1_FILE%" echo     $candidate = $fileName
>> "%PS1_FILE%" echo     $index = 2
>> "%PS1_FILE%" echo     while (Test-Path -LiteralPath (Join-Path $directory $candidate)) {
>> "%PS1_FILE%" echo         $candidate = ('{0} ({1}){2}' -f $baseName, $index, $extension)
>> "%PS1_FILE%" echo         $index++
>> "%PS1_FILE%" echo     }
>> "%PS1_FILE%" echo     return $candidate
>> "%PS1_FILE%" echo }
>> "%PS1_FILE%" echo try {
>> "%PS1_FILE%" echo     if (Test-Path -LiteralPath $zipFile) { Remove-Item -LiteralPath $zipFile -Force }
>> "%PS1_FILE%" echo     $lines = Get-Content -LiteralPath $listFile
>> "%PS1_FILE%" echo     foreach ($raw in $lines) {
>> "%PS1_FILE%" echo         if ($null -eq $raw) { continue }
>> "%PS1_FILE%" echo         $entry = $raw.Trim()
>> "%PS1_FILE%" echo         if ($entry.Length -eq 0) { continue }
>> "%PS1_FILE%" echo         if ($entry.StartsWith('#')) { continue }
>> "%PS1_FILE%" echo         $matches = @()
>> "%PS1_FILE%" echo         if ([System.IO.Path]::IsPathRooted($entry)) {
>> "%PS1_FILE%" echo             if (Test-Path -LiteralPath $entry -PathType Leaf) { $matches = @(Get-Item -LiteralPath $entry) }
>> "%PS1_FILE%" echo         } elseif ($entry.Contains('\') -or $entry.Contains('/')) {
>> "%PS1_FILE%" echo             $entryNormalized = $entry.Replace('/','\')
>> "%PS1_FILE%" echo             $candidate = Join-Path $rootNormalized $entryNormalized
>> "%PS1_FILE%" echo             if (Test-Path -LiteralPath $candidate -PathType Leaf) { $matches = @(Get-Item -LiteralPath $candidate) }
>> "%PS1_FILE%" echo         } else {
>> "%PS1_FILE%" echo             $matches = @(Get-ChildItem -LiteralPath $rootNormalized -Recurse -File -Filter $entry -ErrorAction SilentlyContinue)
>> "%PS1_FILE%" echo         }
>> "%PS1_FILE%" echo         foreach ($file in $matches) {
>> "%PS1_FILE%" echo             if ($null -eq $file) { continue }
>> "%PS1_FILE%" echo             $full = $file.FullName
>> "%PS1_FILE%" echo             if ([string]::IsNullOrWhiteSpace($full)) { continue }
>> "%PS1_FILE%" echo             $full = [System.IO.Path]::GetFullPath($full)
>> "%PS1_FILE%" echo             if ($zipMode -eq 'flat') {
>> "%PS1_FILE%" echo                 $relative = Get-UniqueFlatName -directory $tempDir -fileName $file.Name
>> "%PS1_FILE%" echo             } else {
>> "%PS1_FILE%" echo                 $relative = $file.Name
>> "%PS1_FILE%" echo                 if ($full.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
>> "%PS1_FILE%" echo                     $relative = $full.Substring($rootPrefix.Length)
>> "%PS1_FILE%" echo                 }
>> "%PS1_FILE%" echo                 $relative = $relative.Replace('/','\')
>> "%PS1_FILE%" echo                 if ([string]::IsNullOrWhiteSpace($relative)) { $relative = $file.Name }
>> "%PS1_FILE%" echo             }
>> "%PS1_FILE%" echo             $dest = Join-Path $tempDir $relative
>> "%PS1_FILE%" echo             $destParent = Split-Path -Parent $dest
>> "%PS1_FILE%" echo             if (-not [string]::IsNullOrWhiteSpace($destParent) -and -not (Test-Path -LiteralPath $destParent)) { [void](New-Item -ItemType Directory -Path $destParent -Force) }
>> "%PS1_FILE%" echo             Copy-Item -LiteralPath $full -Destination $dest -Force
>> "%PS1_FILE%" echo             $copiedCount++
>> "%PS1_FILE%" echo         }
>> "%PS1_FILE%" echo     }
>> "%PS1_FILE%" echo     if ($copiedCount -eq 0) { throw 'No files were found from files.txt, so no zip was created.' }
>> "%PS1_FILE%" echo     Compress-Archive -Path (Join-Path $tempDir '*') -DestinationPath $zipFile -Force
>> "%PS1_FILE%" echo }
>> "%PS1_FILE%" echo finally {
>> "%PS1_FILE%" echo     if (Test-Path -LiteralPath $tempDir) { Remove-Item -LiteralPath $tempDir -Recurse -Force }
>> "%PS1_FILE%" echo }

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1_FILE%"
set "ERR=%ERRORLEVEL%"

del "%PS1_FILE%" >nul 2>&1

if not "%ERR%"=="0" (
    echo Failed to create zip.
    pause
    exit /b %ERR%
)

echo Done.
echo Zip file: "%ZIP_FILE%"
pause
