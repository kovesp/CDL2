[string]$target = "\\wsl$\Ubuntu-24.04\home\peter\lab"

Write-Host -ForegroundColor Green "Publishing CDL2 Lab for Windows.."
dotnet publish CDL2v1.csproj       -c Release -r win-x64   --self-contained -p:PublishSingleFile=true

Write-Host -ForegroundColor Green "Publishing CDL2 Lab for Linux.."
dotnet publish CDL2v1-Linux.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

Write-Host -ForegroundColor Green "Copying Linux build to target location.."
Copy-Item bin\Release\net10.0\linux-x64\publish\CDL2v1-Linux $target
Push-Location $target
Write-Host -ForegroundColor Green "Creating tar.gz archive of Linux build.."
tar -czvf cdl2-lab-alpha-1.0.0.tar.gz --exclude=*.tar.gz .
Pop-Location