Write-Host "Starting release build process..."

$zipPath = "JellyfinDiscordAuth.zip"
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


# (Your manifest definition here)
$manifest = @(
    @{
        category    = "Authentication"
        description = "This plugin allows users to sign in with Discord."
        guid        = "359a7d2a-1c54-4e70-abbb-01bc73f098cf"
        name        = "Discord Authentication"
        overview    = "Users can login with Discord"
        owner       = "EvanTrow"
        versions    = @(
            @{
                changelog = "1.0.0.0: Initial Release....`n"
                checksum  = (Get-FileHash $zipPath -Algorithm MD5).Hash
                sourceUrl = "http://192.168.1.10:56080/jellyfin/discord/JellyfinDiscordAuth.zip"
                targetAbi = "10.11.0.0"
                timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                version   = "1.0.0.0"
            }
        )
    }
)

ConvertTo-Json -InputObject @($manifest) -Depth 6 | Set-Content -Encoding UTF8 "manifest.json"

Write-Host "Release file $zipPath and manifest.json generated."