param([Parameter(Mandatory)][string]$OutputDirectory, [string]$Base)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath (Join-Path $output 'builds')) { throw 'BuildPair refuses to overwrite existing builds.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location $repo
try {
    $fingerprint = & python (Join-Path $PSScriptRoot 'source_fingerprint.py') | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Source fingerprint failed.' }
    $fingerprint | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'source-fingerprint.json') -Encoding utf8
    if (-not $Base) {
        $reference = Get-Content (Join-Path $repo '.kias-benchmark/BASELINE_MANIFEST.json') -Raw | ConvertFrom-Json
        $Base = $reference.base
    }
    $base = (& git merge-base $Base HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine common source baseline.' }
    & python (Join-Path $PSScriptRoot 'prepare_pair.py') --directory $output --base $base *> (Join-Path $output 'prepare.log')
    if ($LASTEXITCODE -ne 0) { throw "Matched source preparation failed: $output/prepare.log" }
    $manifestPath = Join-Path $output 'BASELINE_MANIFEST.json'
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest | Add-Member -NotePropertyName sourceFingerprint -NotePropertyValue $fingerprint.sha256
    $manifest | Add-Member -NotePropertyName sdk -NotePropertyValue ((& dotnet --version).Trim())
    foreach ($build in $manifest.builds) {
        Write-Host "Сборка Release $($build.label)..."
        Push-Location $build.source
        try { & dotnet build Content.Server/Content.Server.csproj -c Release -m:4 -nr:false *> (Join-Path $output "build-$($build.label).log") }
        finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { throw "Release build $($build.label) failed; performance run is forbidden." }
        & python (Join-Path $PSScriptRoot 'instrument_headless_client.py') (Join-Path $build.source 'RobustToolbox') *> (Join-Path $output "client-instrumentation-$($build.label).json")
        if ($LASTEXITCODE -ne 0) { throw 'Headless client instrumentation failed.' }
        Push-Location $build.source
        try { & dotnet build Content.Client/Content.Client.csproj -c Release -m:4 -nr:false *> (Join-Path $output "build-client-$($build.label).log") }
        finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { throw "Matched client build $($build.label) failed." }
        $taskClientHashes=@{}
        foreach ($taskAssembly in Get-ChildItem -LiteralPath (Join-Path $build.source 'bin/Content.Client') -Filter '*.dll') {
            $taskClientHashes[$taskAssembly.Name]=(Get-FileHash -LiteralPath $taskAssembly.FullName).Hash.ToLowerInvariant()
        }
        $build | Add-Member -NotePropertyName clientRuntimeAssemblyHashes -NotePropertyValue $taskClientHashes
        $build.compiled = $true
        $taskRuntimeHashes = @{}
        foreach ($taskAssembly in Get-ChildItem -LiteralPath (Join-Path $build.source 'bin/Content.Server') -Filter '*.dll') {
            $taskRuntimeHashes[$taskAssembly.Name] = (Get-FileHash -LiteralPath $taskAssembly.FullName).Hash.ToLowerInvariant()
        }
        $build | Add-Member -NotePropertyName runtimeAssemblyHashes -NotePropertyValue $taskRuntimeHashes
        foreach ($entry in @{
            assemblySha256 = 'bin/Content.Server/Content.Server.dll'
            nativeLabSha256 = 'Tools/KiasBenchmark/Server/NativeLab.cs'
            nativeGameDriversSha256 = 'Tools/KiasBenchmark/Server/NativeGameDrivers.cs'
        }.GetEnumerator()) {
            $build | Add-Member -NotePropertyName $entry.Key -NotePropertyValue (Get-FileHash (Join-Path $build.source $entry.Value)).Hash.ToLowerInvariant()
        }
        if ($build.label -eq 'B') {
            $build | Add-Member -NotePropertyName telemetrySha256 -NotePropertyValue (Get-FileHash (Join-Path $build.source 'Tools/KiasBenchmark/Server/KiasTelemetry.cs')).Hash.ToLowerInvariant()
        }
        $manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -Encoding utf8
    }
    $current = & python (Join-Path $PSScriptRoot 'source_fingerprint.py') | ConvertFrom-Json
    if ($current.sha256 -ne $fingerprint.sha256) { throw 'REBUILD_REQUIRED: source changed while compiling the pair.' }
    $manifest.status = 'MATCHED_RELEASE_BUILDS_COMPLETE_RUNTIME_NOT_TESTED'
    $manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -Encoding utf8
    Write-Host "Готовы свежие A/B. Манифест: $manifestPath"
} catch {
    @{ status='FAIL'; error=$_.Exception.Message } | ConvertTo-Json | Set-Content (Join-Path $output 'build-failed.json') -Encoding utf8
    throw
} finally { Pop-Location }
