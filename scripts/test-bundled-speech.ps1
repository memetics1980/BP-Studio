$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$savedEnvironment=@{}
foreach($name in @('DOTNET_ROOT','DOTNET_CLI_HOME','NUGET_PACKAGES')) { $savedEnvironment[$name]=[Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $dotnet=Join-Path $projectRoot '.cache\dotnet\dotnet.exe'
    if(-not (Test-Path -LiteralPath $dotnet)) { $dotnet=(Get-Command dotnet).Source }
    $env:DOTNET_ROOT=Split-Path -Parent $dotnet
    $env:DOTNET_CLI_HOME=Join-Path $projectRoot '.cache\dotnet-home'
    $env:NUGET_PACKAGES=Join-Path $projectRoot '.cache\nuget'
    & $dotnet run --project (Join-Path $projectRoot 'native\BPStudio.Speech.Tests') -- $projectRoot
    if($LASTEXITCODE -ne 0) { throw 'Bundled speech verification failed.' }
} finally {
    foreach($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name],'Process') }
}
