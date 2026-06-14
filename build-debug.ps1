param(
    [string]$JellyfinExe = $(if ($env:JELLYFIN_EXE_PATH) { $env:JELLYFIN_EXE_PATH } else { "C:\Program Files\Jellyfin\Server\jellyfin.exe" }),
    [string]$JellyfinDataPath = $(if ($env:JELLYFIN_DATA_PATH) { $env:JELLYFIN_DATA_PATH } else { Join-Path $env:LOCALAPPDATA "jellyfin" })
)

$meta = Get-Content "meta.json" -Raw | ConvertFrom-Json
$version = $meta.version
$pluginName = $meta.name -replace '[\\/:*?"<>|]', '-'

$pluginsRoot = Join-Path $JellyfinDataPath "plugins"
$destination = Join-Path $pluginsRoot "${pluginName}_${version}"
$sourceDir = "bin\Debug\net9.0"

$filesToCopy = @(
    "JellyfinDiscordAuth.dll",
    "Discord.Net.Commands.dll",
    "Discord.Net.Core.dll",
    "Discord.Net.Interactions.dll",
    "Discord.Net.Rest.dll",
    "Discord.Net.Webhook.dll",
    "Discord.Net.WebSocket.dll"
)

$jellyfinProc = Get-Process -Name "jellyfin" -ErrorAction SilentlyContinue
if ($jellyfinProc) {
    Write-Host "Stopping jellyfin.exe..."
    Stop-Process -Name "jellyfin" -Force
    Start-Sleep -Seconds 2
}

# Remove all existing copies of this plugin to prevent Jellyfin from loading duplicates
Get-ChildItem -Path $pluginsRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "${pluginName}_*" } |
    ForEach-Object {
        Write-Host "Removing old plugin directory: $($_.Name)"
        Remove-Item $_.FullName -Recurse -Force
    }

New-Item -ItemType Directory -Path $destination | Out-Null

foreach ($file in $filesToCopy) {
    $src = Join-Path $sourceDir $file
    if (Test-Path $src) {
        Copy-Item $src -Destination $destination -Force
        Write-Host "Copied $file"
    }
    else {
        Write-Warning "File not found: $src"
    }
}

Write-Host "Debug copy complete to: $destination"

if ($jellyfinProc) {
    Write-Host "Restarting jellyfin.exe..."
    Start-Process -FilePath $JellyfinExe
}
