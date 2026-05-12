<#
.SYNOPSIS
   Generate a Windows and a Linux executable for the CDL2 lab and package for Linux.
.DESCRIPTION
   The script copies required files for distribution and generates 3 zip files:
   * CDL2-Windows.zip: Contains the Windows executables and all other files, but not the Linux executables.
   * CDL2-Linux.zip: Contains the Linux executables and all other files, but not the Windows executables.
   * CDL2.zip: Contains both the Windows and Linux executables and all other files.
   
   The script also copies itself to the VisualSutdio project directory so it is pushed to GitHub.
.PARAMETER OS
   The operating system(s) to generate the lab for. Default is both Windows and Linux.
.PARAMETER NoWSL
   Skip copying the build to the WSL test location. Default is to copy the build.
.EXAMPLE
   publish-cdl2
#>

#cspell:ignore czvf labc

[CmdletBinding()]
param (
   [switch]$NoBuild,
   [ValidateSet('windows', 'linux')]
   [string[]]$OS = ('windows','linux'),
   [switch]$noWSL,
   [switch]$KeepPDBs
)

[string]$source       = 'C:\Visual Studio Projects\CDL2\CDL2v1'
[string]$WSLDir       = "/home/peter/lab"
[string]$WSLTarget    = Join-Path '\\wsl$\Ubuntu-24.04' $WSLDir

[bool]$Windows        = $OS -contains 'windows'
[bool]$Linux          = $OS -contains 'linux'

[string]$targetDir    = "$Env:temp\CDL2Release"
[string]$targetRoot   = 'cdl2'
[string]$releaseDir   = 'Release'

# Note: The file map is used to determine which files to copy for each part.
#       * The keys are the file names.
#       * The value may be the empty string (use $source\$targetRoot), or the directory of the file relative to $source.
#       * The executables get special treatment (generated in place, or temp directory to be copied when the zip is built)
[Hashtable]$FileMap = @{
   _README                        = '.'
   _LICENSE                       = '.'
   Docs = @{
      'CDL2 Lab.md'               = ''
      'CDL2-vWG.md'               = ''
      'hms.pdf'                   = ''
      'Implementation Notes.md'   = ''
      'md-styles.css'             = ''
   }
   Sample = @{
      'CDL2v1.lab.gz'             = ''
      'Quicksort.cdl2'            = ''
      'LabCommandsTest.labc'      = ''
   }
   C = @{
      'CDL2.h'                    = ''
      'CDL2Trace.h'               = ''
   }
   'bin\Windows' = @{
      'cc.cmd'                    = ''
      'cco.cmd'                   = ''
      'cdl2-lab.cmd'              = ''
      'cdl2c.cmd'                 = ''
   }
   'bin\Linux' = @{
      'cdl2-lab'                  = ''
      'cdl2c'                     = ''
   }   
}
# Copy the files in $map to the target given by $dst. The keys of $map are the file names.
# The value of $map is a hashtable with the same structure as $FileMap.
function Copy-Files([string]$dst,[Hashtable]$map) {
   [string]$dstDir = $dst -replace [regex]::escape("$targetDir\$targetRoot"),''
   foreach ($fn in $map.Keys | Sort-Object) {
      $src = $map[$fn]
      if ($src -is [string]) {
         [string]$srcPath = ''
         if ($src -eq '') {
            $srcPath = Join-Path $source "$targetRoot\$fn"
         } else {
            $srcPath = Join-Path $source "$(if ($src -eq '.') { '' } else { $src })\$fn"
         }
         $fileName = $srcPath -replace "$([Regex]::Escape($source))\\",''
         if ($fileName.StartsWith($targetRoot)) {
            $fileName = $fileName -replace "^$targetRoot","$targetRoot$dstDir"
         }
         Write-Host -ForegroundColor Yellow "   $filename"
         New-Item -ItemType Directory -Force -Path $dst -ErrorAction Ignore | Out-Null
         Copy-Item $srcPath $dst -Force
      } else {
         New-Item -ItemType Directory -Force -Path "$dst\$fn" -ErrorAction Ignore | Out-Null
         Copy-Files "$dst\$fn" $map[$fn]
      } 
   }  
}

Remove-Item -Recurse -Force $targetDir -ErrorAction Ignore
New-Item -ItemType Directory -Force -Path "$targetDir\$targetRoot" -ErrorAction Ignore | Out-Null

Write-Host -ForegroundColor Green "Copying files $source -> $($targetDir -replace [Regex]::Escape($Env:TMP),'$env:TMP') ..."
Copy-Files "$targetDir\$targetRoot" $FileMap

Push-Location $source
if (-not $NoBuild -and $Windows) {
   Write-Host -ForegroundColor Green "`nPublishing CDL2 Lab for Windows..."
   dotnet publish CDL2v1.csproj        -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o:"$targetDir\$targetRoot\bin\Windows"
   if (-not $KeepPDBs) { Remove-Item -Force "$targetDir\$targetRoot\bin\Windows\*.pdb" -ErrorAction Ignore }
}

if (-not $NoBuild -and $Linux) {
   Write-Host -ForegroundColor Green "`nPublishing CDL2 Lab for Linux..."
   dotnet publish CDL2v1-Linux.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o:"$targetDir\Linux"
   if (-not $KeepPDBs) { Remove-Item -Force "$targetDir\Linux\*.pdb" -ErrorAction Ignore }
}
Pop-Location

New-Item -ItemType Directory -Force -Path "$source\$releaseDir" -ErrorAction Ignore | Out-Null
Remove-Item -Force "$source\$releaseDir\*.zip" -ErrorAction Ignore

# Create the Windows only version zip file
Write-Host -ForegroundColor Green "`nCreating Windows only zip file (CDL2-Windows.zip) ..."
Compress-Archive -Path "$targetDir\$targetRoot" -DestinationPath "$source\$releaseDir\CDL2-Windows.zip" -Force
zip -d "$source\$releaseDir\CDL2-Windows.zip" "cdl2/bin/Linux/*" *> $null

# Copy the Linux directory to the target to create the combined version zip file.
Write-Host -ForegroundColor Green "Creating combined version zip file (CDL2.zip) ..."
Move-Item -Force "$targetDir\Linux\*" "$targetDir\$targetRoot\bin\Linux"
Compress-Archive -Path "$targetDir\$targetRoot" -DestinationPath "$source\$releaseDir\CDL2.zip" -Force

# Remove the Windows directory to create the Linux only version zip file.
Write-Host -ForegroundColor Green "Creating Linux only zip file (CDL2-Linux.zip) ..."
Remove-Item -Recurse -Force "$targetDir\$targetRoot\bin\Windows" -ErrorAction Ignore
Compress-Archive -Path "$targetDir\$targetRoot" -DestinationPath "$source\$releaseDir\CDL2-Linux.zip" -Force

Write-Host -ForegroundColor Green "Zip files created"
Get-ChildItem -Path "$source\$releaseDir\*.zip"
 
if (-not $noWSL -and $Linux) {
   Write-Host -ForegroundColor Green "`nCopying Linux build to WSL test location ($WSLDir) and making it executable ..."
   Copy-Item -Force "$source\$releaseDir\CDL2-Linux.zip" $WSLTarget
   WSL -d Ubuntu-24.04 unzip -o "$WSLDir/CDL2-Linux.zip" -d $WSLDir
   [string]$WSLBin = "$WSLDir/cdl2/bin/Linux"
   WSL -d Ubuntu-24.04 chmod +x "$WSLBin/cdl2-lab" "$WSLBin/cdl2c" "$WSLBin/CDL2v1-Linux"
}

Write-Host -ForegroundColor Green "`nCopying publish script to Visual Studio project directory for GitHub ..."
Copy-Item -Force $PSCommandPath 'C:\Visual Studio Projects\CDL2\CDL2v1\CDL2\'
