dotnet.exe build
$compress = @{
    Path = "bin/Debug/net48/Randomizer.dll", "Info.json", "icons/"
    CompressionLevel = "Fastest"
    DestinationPath = "Randomizer.zip"
}
Compress-Archive -Force @compress