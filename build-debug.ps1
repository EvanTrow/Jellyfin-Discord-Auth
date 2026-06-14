param(
    [string]$JellyfinExe = $(if ($env:JELLYFIN_EXE_PATH) { $env:JELLYFIN_EXE_PATH } else { "C:\Program Files\Jellyfin\Server\jellyfin.exe" })
)

$destination = "C:\Users\evan\AppData\Local\jellyfin\plugins\Discord Authentication_1.0.0.0"
$sourceDir = "bin\Debug\net9.0"
$jellyfinExe = $JellyfinExe
$pluginsRoot = Join-Path $env:LOCALAPPDATA "jellyfin\plugins"
$destination = Join-Path $pluginsRoot ("Discord Authentication_{0}" -f $pluginVersion)
$sourceDir = "bin\Debug\net9.0"
$jellyfinExe = "C:\Program Files\Jellyfin\Server\jellyfin.exe"

$filesToCopy = @(
    "JellyfinDiscordAuth.dll",
    "Discord.Net.Commands.dll",
    "Discord.Net.Core.dll",
    "Discord.Net.Interactions.dll",
    "Discord.Net.Rest.dll",
    "Discord.Net.Webhook.dll",
    "Discord.Net.WebSocket.dll"
)

if (-not (Test-Path $destination)) {
    New-Item -ItemType Directory -Path $destination | Out-Null
}

# Stop Jellyfin if running
$jellyfinProc = Get-Process -Name "jellyfin" -ErrorAction SilentlyContinue
if ($jellyfinProc) {
    Write-Host "Stopping jellyfin.exe..."
    Stop-Process -Name "jellyfin" -Force
    Start-Sleep -Seconds 2
}

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

Write-Host "Debug copy complete."

# Restart Jellyfin if it was running
Write-Host "Restarting jellyfin.exe..."
Start-Process -FilePath $jellyfinExe

