
$projects = @(
    "BNAPI/BNAPI/BNAPI.csproj",
    "BadNorthAxeThrower/BadNorthAxeThrower/BadNorthAxeThrower.csproj",
    "BadNorthCheaperClass/BadNorthCheaperClass/BadNorthCheaperClass.csproj",
    "BadNorthRegenerative/BadNorthRegenerative/BadNorthRegenerative.csproj",
    "BadNorthThorns/BadNorthThorns/BadNorthThorns.csproj"
)

foreach ($proj in $projects) {
    Write-Host "Building $proj ..."
    dotnet build $proj -c Debug
}
Write-Host "All projects built."
