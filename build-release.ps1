Write-Host "Starting release build process..."

$meta = Get-Content "meta.json" -Raw | ConvertFrom-Json
$version = $meta.version
$releaseDir = "Release\$version"
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

$zipPath = "$releaseDir\JellyfinDiscordAuth.zip"
$filesToZip = @(
    "bin\Debug\net9.0\JellyfinDiscordAuth.dll", 
    "bin\Debug\net9.0\Discord.Net.Webhook.dll", 
    "bin\Debug\net9.0\Discord.Net.WebSocket.dll", 
    "bin\Debug\net9.0\Discord.Net.Interactions.dll", 
    "bin\Debug\net9.0\Discord.Net.Rest.dll", 
    "bin\Debug\net9.0\Discord.Net.Commands.dll", 
    "bin\Debug\net9.0\Discord.Net.Core.dll", 
    "meta.json")

if (Test-Path $zipPath) {
    Remove-Item $zipPath
}

$existingFiles = @()
foreach ($file in $filesToZip) {
    if (Test-Path $file) {
        $existingFiles += $file
    }
    else {
        Write-Warning "File not found: $file"
    }
}

if ($existingFiles.Count -gt 0) {
    Compress-Archive -Path $existingFiles -DestinationPath $zipPath
}
else {
    Write-Warning "No files to zip."
}


# Load existing manifest versions to preserve history
$manifestPath = "manifest.json"
$existingVersions = @()
if (Test-Path $manifestPath) {
    $existingManifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if ($existingManifest -and $existingManifest[0].versions) {
        $existingVersions = $existingManifest[0].versions
    }
}

$newVersion = @{
    changelog = $meta.changelog
    checksum  = (Get-FileHash $zipPath -Algorithm MD5).Hash
    sourceUrl = "http://192.168.1.10:56080/jellyfin/discord/Release/$version/JellyfinDiscordAuth.zip"
    targetAbi = "10.11.0.0"
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    version   = $version
}

$allVersions = @($newVersion) + $existingVersions

$manifest = @(
    @{
        category    = "Authentication"
        description = "This plugin allows users to sign in with Discord."
        guid        = "359a7d2a-1c54-4e70-abbb-01bc73f098cf"
        name        = "Discord Authentication"
        overview    = "Users can login with Discord"
        owner       = "EvanTrow"
        versions    = $allVersions
    }
)

ConvertTo-Json -InputObject @($manifest) -Depth 6 | Set-Content -Encoding UTF8 $manifestPath

Write-Host "Release file $zipPath and manifest.json generated."