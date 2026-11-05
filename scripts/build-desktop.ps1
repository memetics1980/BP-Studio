param([switch]$Publish, [switch]$Run, [switch]$SkipInstall, [switch]$BundleWebView)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$savedEnvironment = @{}
foreach ($name in @('DOTNET_ROOT','DOTNET_CLI_HOME','NUGET_PACKAGES','DOTNET_CLI_TELEMETRY_OPTOUT')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
Push-Location $projectRoot
try {
    $localDotnet = Join-Path $projectRoot '.cache\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet) { $dotnet=$localDotnet; $env:DOTNET_ROOT=Split-Path -Parent $localDotnet } else { $dotnet=(Get-Command dotnet -ErrorAction Stop).Source }
    $env:DOTNET_CLI_HOME=Join-Path $projectRoot '.cache\dotnet-home'
    $env:NUGET_PACKAGES=Join-Path $projectRoot '.cache\nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
    & python scripts/prepare-speech-bundle.py
    if ($LASTEXITCODE -ne 0) { throw 'Bundled speech runtime preparation failed.' }
    if ($BundleWebView) {
        & python scripts/prepare-webview-bundle.py
        if ($LASTEXITCODE -ne 0) { throw 'Bundled WebView2 preparation failed.' }
    }
    if (-not $SkipInstall) {
        & pnpm --dir ui install --frozen-lockfile
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency installation failed.' }
    }
    & pnpm --dir ui build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    $project='native/BPStudio.Desktop/BPStudio.Desktop.csproj'
    & $dotnet restore $project --force -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }
    if ($Publish) {
        [xml]$projectXml=Get-Content -LiteralPath $project -Raw
        $version=($projectXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
        $output='release/BP-Studio-'+$version+'-Desktop-Windows-x64'
        & $dotnet publish $project -c Release --no-restore -o $output -v minimal
        if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }
        foreach ($file in @('README.md','桌面工作台使用说明.md','桌面工作台验证记录.md','THIRD_PARTY_SPEECH.md','THIRD_PARTY_DESKTOP.md','首次使用说明.txt')) {
            if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination $output -Force }
        }
        $exe=Join-Path $output 'BPStudio.exe'
    } else {
        & $dotnet build $project --no-restore -v minimal
        if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
        $exe='native/BPStudio.Desktop/bin/Debug/net8.0-windows/win-x64/BPStudio.exe'
    }
    # A runnable directory is the normal deliverable. Archives are created only on explicit request.
    if ($Run) { Start-Process -FilePath (Join-Path $projectRoot $exe) -WindowStyle Normal }
} finally {
    Pop-Location
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name],'Process') }
}
