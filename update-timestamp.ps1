$path = (Get-Item "meta.json").FullName
$content = Get-Content $path -Raw
$timestamp = [datetime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$updated = $content -replace '(?<="timestamp":\s*")[^"]*', $timestamp
[IO.File]::WriteAllText($path, $updated)
