param([string]$OutputDirectory = 'artifacts')
$ErrorActionPreference = 'Stop'
$legacyMsbuild = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\MSBuild.exe'
$xamlTargets = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\Microsoft\WindowsXaml\v14.0\Microsoft.Windows.UI.Xaml.CSharp.targets'
$phoneReferences = Join-Path ${env:ProgramFiles(x86)} 'Windows Phone Kits\8.1\References'
if (!(Test-Path -LiteralPath $legacyMsbuild) -or !(Test-Path -LiteralPath $xamlTargets) -or !(Test-Path -LiteralPath $phoneReferences)) {
    throw 'WP8.1 toolchain missing. Install Visual Studio 2015 with Windows Phone 8.1 tools on a self-hosted Windows runner labelled wp81. setup-msbuild alone does not install this SDK.'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $legacyMsbuild 'src\Vianium.LocalSend\Vianium.LocalSend.csproj' /t:Rebuild /m /p:Configuration=Release /p:Platform=ARM /p:VisualStudioVersion=14.0 /p:AppxPackageSigningEnabled=false /p:AppxBundle=Never /p:GenerateAppxPackageOnBuild=true "/p:AppxPackageDir=$((Resolve-Path $OutputDirectory).Path)\" /fl "/flp:logfile=$OutputDirectory\build.log;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "ARM build failed: $LASTEXITCODE" }
$packages = Get-ChildItem -LiteralPath $OutputDirectory -Recurse -Filter '*.appx'
if (!$packages) { throw 'Build finished without an APPX output.' }
