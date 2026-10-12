$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet build "$root\player\XhznPlayer.sln" -c Release
dotnet run --project "$root\player\tools\PackBuilder\PackBuilder.csproj" -c Release --no-build
if ($LASTEXITCODE -ne 0) {
    dotnet run --project "$root\player\tools\PackBuilder\PackBuilder.csproj" -c Release
}

dotnet publish "$root\player\src\XhznPlayer\XhznPlayer.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true

Write-Host "发布目录: $root\player\src\XhznPlayer\bin\Release\net9.0-windows\win-x64\publish"
Write-Host "用 Inno Setup 编译 installer\xhzn-player.iss 打安装包。"
Write-Host "把 samples\golden.xhznpack 拷到 %ProgramData%\XHZN\projects\ 即可联调。"
