$ErrorActionPreference = 'Stop'
$rulesWorkspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$rulesDirectory = Join-Path $rulesWorkspace 'Temp/DifferenceInterstitialRulesCheck'
New-Item -ItemType Directory -Path $rulesDirectory -Force | Out-Null
$rulesProduction = [System.Security.SecurityElement]::Escape((Join-Path $rulesWorkspace 'Assets/Scripts/Game/DifferenceAds.cs'))
$rulesFixture = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'DifferenceInterstitialRulesCheck.cs'))
$rulesProject = Join-Path $rulesDirectory 'DifferenceInterstitialRulesCheck.csproj'
$rulesProjectXml = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <EnableDifferenceSdk Condition="'$(EnableDifferenceSdk)' == ''">true</EnableDifferenceSdk>
    <DefineConstants>DIFFERENCE_AD_RULES_ISOLATED</DefineConstants>
    <DefineConstants Condition="'$(EnableDifferenceSdk)' == 'true'">$(DefineConstants);ENABLE_DIFFERENCE_SDK</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="PRODUCTION_SOURCE" />
    <Compile Include="FIXTURE_SOURCE" />
  </ItemGroup>
</Project>
'@
$rulesProjectXml.Replace('PRODUCTION_SOURCE', $rulesProduction).Replace('FIXTURE_SOURCE', $rulesFixture) |
    Set-Content -LiteralPath $rulesProject -Encoding utf8
$rulesReport = Join-Path $rulesWorkspace 'Temp/FindDifferences-interstitial-rules.txt'
$rulesOutput = @()
foreach ($rulesSdk in @('true', 'false')) {
    $rulesBuildOutput = Join-Path $rulesDirectory ('bin-' + $rulesSdk)
    $rulesBuild = @(& dotnet build $rulesProject --configuration Release --nologo --verbosity quiet --ignore-failed-sources "-p:EnableDifferenceSdk=$rulesSdk" --output $rulesBuildOutput 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $rulesBuild | Set-Content -LiteralPath $rulesReport -Encoding utf8
        throw ('Isolated ad rules compilation failed. See ' + $rulesReport)
    }
    $rulesRun = @(& dotnet (Join-Path $rulesBuildOutput 'DifferenceInterstitialRulesCheck.dll') 2>&1)
    $rulesOutput += $rulesRun
    $rulesOutput | Set-Content -LiteralPath $rulesReport -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw ('Isolated ad rules check failed. See ' + $rulesReport) }
}
$rulesOutput
