# install-dotnet-simple.ps1
# 使用Windows Package Manager安装.NET 8 SDK

Write-Host "正在使用winget安装.NET 8 SDK..."

# 检查是否安装了winget
if (Get-Command winget -ErrorAction SilentlyContinue) {
    Write-Host "使用winget安装.NET 8 SDK..."
    winget install Microsoft.DotNet.SDK.8
} else {
    Write-Host "winget未找到，请手动安装.NET 8 SDK"
    Write-Host "下载地址: https://dotnet.microsoft.com/download/dotnet/8.0"
    Write-Host "或运行以下命令下载安装程序:"
    Write-Host "Invoke-WebRequest -Uri 'https://download.visualstudio.microsoft.com/download/pr/8b92f71a-c2a1-4e58-a6b7-0e6b3b1e3b3a/dotnet-sdk-8.0.404-win-x64.exe' -OutFile '$env:TEMP\dotnet-sdk-8.0.404-win-x64.exe'"
    Write-Host "然后运行安装程序"
}

Write-Host "安装完成后，请重新启动终端并运行: dotnet --version"