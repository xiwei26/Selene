# install-dotnet.ps1
# Install .NET 8 SDK

Write-Host "Installing .NET 8 SDK..."

# Download .NET SDK installer
$installerUrl = "https://download.visualstudio.microsoft.com/download/pr/8b92f71a-c2a1-4e58-a6b7-0e6b3b1e3b3a/dotnet-sdk-8.0.404-win-x64.exe"
$installerPath = "$env:TEMP\dotnet-sdk-8.0.404-win-x64.exe"

Write-Host "Downloading .NET SDK installer..."
Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath

Write-Host "Running installer..."
Start-Process -FilePath $installerPath -ArgumentList "/install", "/quiet", "/norestart" -Wait

Write-Host "Cleaning up..."
Remove-Item $installerPath -Force

Write-Host ".NET 8 SDK installed successfully!"
Write-Host "Please restart your terminal and run: dotnet --version"