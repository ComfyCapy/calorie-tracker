[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [ValidateSet('linux-x64')]
    [string]$RuntimeIdentifier = 'linux-x64'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'CalorieTracker/CalorieTracker.csproj'
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = [IO.Path]::GetFullPath((Join-Path $artifactsRoot 'publish'))
$archivePath = [IO.Path]::GetFullPath((Join-Path $artifactsRoot 'comfycapy-release.zip'))
$expectedPublishDirectory = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/publish'))

if ($publishDirectory -ne $expectedPublishDirectory) {
    throw 'The publish directory resolved outside the repository artifacts folder.'
}

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Project file was not found."
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

# Always publish into a fresh directory so stale files and prior publish output
# cannot become part of a release or recursively include the release itself.
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

& dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained false `
    --output $publishDirectory `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$forbiddenPattern = '(^|[\\/])(\.git|\.vs|bin|obj|artifacts|node_modules|ClientApp)([\\/]|$)|(^|[\\/])\.env($|\.)|\.(db|sqlite)(-wal|-shm)?$|\.(pem|key|pfx)$'
$developmentSettingsPattern =
    '(^|[\\/])appsettings\.Development\.json$'
$forbiddenEntries = Get-ChildItem -LiteralPath $publishDirectory -Recurse -Force |
    Where-Object {
        $relativePath = $_.FullName.Substring($publishDirectory.Length).TrimStart('\', '/')
        $relativePath -match $forbiddenPattern -or
            $relativePath -match $developmentSettingsPattern
    }

if ($forbiddenEntries) {
    $names = $forbiddenEntries | ForEach-Object { $_.FullName.Substring($publishDirectory.Length).TrimStart('\', '/') }
    throw "Publish output contains forbidden deployment entries: $($names -join ', ')"
}

if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'wwwroot/lib') -PathType Container) -or
    -not (Test-Path -LiteralPath (Join-Path $publishDirectory 'wwwroot/images') -PathType Container) -or
    -not (Test-Path -LiteralPath (Join-Path $publishDirectory 'wwwroot/Identity/lib') -PathType Container)) {
    throw 'Publish output is missing one or more required static-asset directories.'
}

$requiredFiles = @(
    'wwwroot/lib/jquery/dist/jquery.min.js',
    'wwwroot/lib/bootstrap/dist/css/bootstrap.min.css',
    'wwwroot/lib/jquery-validation/dist/jquery.validate.min.js',
    'wwwroot/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js',
    'wwwroot/Identity/lib/jquery-validation/dist/jquery.validate.min.js',
    'wwwroot/Identity/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js',
    'wwwroot/images/capy/expressions/Capy-Base.png'
)

foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $relativePath) -PathType Leaf)) {
        throw "Publish output is missing required file: $relativePath"
    }
}

# Compress only the directory contents. The Linux extraction script restores
# explicit modes because ZIP archives created on Windows do not reliably carry
# Unix directory and executable bits.
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal

Write-Host "Published clean $RuntimeIdentifier release to $publishDirectory"
Write-Host "Created deployment archive at $archivePath"
