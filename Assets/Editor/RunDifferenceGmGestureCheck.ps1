$ErrorActionPreference = 'Stop'
$gestureWorkspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$gestureDirectory = Join-Path $gestureWorkspace 'Temp/DifferenceGmGestureCheck'
New-Item -ItemType Directory -Path $gestureDirectory -Force | Out-Null
$gestureProduction = [System.Security.SecurityElement]::Escape((Join-Path $gestureWorkspace 'Assets/Hotfix/UI/Logic/UIDifferences/DifferenceGmGesture.cs'))
$gestureFixture = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'DifferenceGmGestureCheck.cs'))
$gestureProject = Join-Path $gestureDirectory 'DifferenceGmGestureCheck.csproj'
$gestureProjectXml = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <DefineConstants>ENABLE_UI_UGUI;DIFFERENCE_GM_GESTURE_ISOLATED</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="PRODUCTION_SOURCE" />
    <Compile Include="FIXTURE_SOURCE" />
  </ItemGroup>
</Project>
'@
$gestureProjectXml.Replace('PRODUCTION_SOURCE', $gestureProduction).Replace('FIXTURE_SOURCE', $gestureFixture) |
    Set-Content -LiteralPath $gestureProject -Encoding utf8
$gestureReport = Join-Path $gestureWorkspace 'Temp/FindDifferences-gm-gesture.txt'
$gestureBuildOutput = Join-Path $gestureDirectory 'bin'
$gestureBuild = @(& dotnet build $gestureProject --configuration Release --nologo --verbosity quiet --ignore-failed-sources --output $gestureBuildOutput 2>&1)
if ($LASTEXITCODE -ne 0) {
    $gestureBuild | Set-Content -LiteralPath $gestureReport -Encoding utf8
    throw ('Isolated GM gesture compilation failed. See ' + $gestureReport)
}
$gestureResult = @(& dotnet (Join-Path $gestureBuildOutput 'DifferenceGmGestureCheck.dll') 2>&1)
$gestureResult | Set-Content -LiteralPath $gestureReport -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw ('Isolated GM gesture check failed. See ' + $gestureReport) }
$gestureResult
