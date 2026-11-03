param([switch]$Preview)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$outputRoot = Join-Path $projectRoot ('Temp/LubanGenerated/' + [guid]::NewGuid().ToString('N'))
$dataDir = Join-Path $outputRoot 'Data'
$codeDir = Join-Path $outputRoot 'Code'
New-Item -ItemType Directory -Force $dataDir, $codeDir | Out-Null

Push-Location $PSScriptRoot
try {
    & dotnet ./Tools/Luban.dll --target client --dataTarget json --codeTarget cs-simple-json `
        --xargs "outputDataDir=$dataDir" --xargs "outputCodeDir=$codeDir" `
        --xargs 'tableImporter.name=gameframex' -x 'l10n.provider=gameframex' `
        -x 'l10n.textFile.keyFieldName=key' -x 'l10n.textFile.path=./Excels/Local/' --conf ./luban.conf
    if ($LASTEXITCODE -ne 0) { throw "Luban 生成失败，退出码 $LASTEXITCODE" }

    foreach ($name in @('Localization.cs', 'TbLocalization.cs')) {
        $path = Join-Path $codeDir ('Local/' + $name)
        if (!(Test-Path -LiteralPath $path)) { throw "Missing generated file: $path" }
        $text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
        $text = [regex]::Replace($text, '(?m)[ \t]+(?=\r?$)', '')
        [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
    }
    $generatedJson = Join-Path $dataDir 'local_tblocalization.json'
    $targetJson = Join-Path $projectRoot 'Assets/Bundles/Config/local_tblocalization.json'
    $rows = Get-Content -Raw -Encoding UTF8 -LiteralPath $generatedJson | ConvertFrom-Json
    $keys = @{}
    foreach ($row in $rows) {
        if ([string]::IsNullOrWhiteSpace($row.key) -or $keys.ContainsKey($row.key)) { throw "生成表存在空键或重复键：$($row.key)" }
        $keys[$row.key] = $true
        $expected = @([regex]::Matches([string]$row.English, '\{\d+(?:[^{}]*)\}') | ForEach-Object { $_.Value } | Sort-Object)
        foreach ($field in @('ChineseSimplified', 'ChineseTraditional', 'French', 'German', 'Indonesian', 'Japanese', 'Korean', 'PortugueseBrazil', 'PortuguesePortugal', 'Russian', 'SpanishLatinAmerica', 'SpanishSpain', 'Thai', 'Vietnamese', 'Hindi')) {
            $value = [string]$row.$field
            if ([string]::IsNullOrWhiteSpace($value)) { continue }
            $actual = @([regex]::Matches($value, '\{\d+(?:[^{}]*)\}') | ForEach-Object { $_.Value } | Sort-Object)
            if (($actual -join '|') -ne ($expected -join '|')) { throw "$($row.key) 的 $field 占位符与英文不一致" }
        }
    }
    if (Test-Path -LiteralPath $targetJson) {
        $previous = Get-Content -Raw -Encoding UTF8 -LiteralPath $targetJson | ConvertFrom-Json
        $missing = @($previous | Where-Object { -not $keys.ContainsKey($_.key) } | ForEach-Object { $_.key })
        if ($missing.Count -gt 0) { throw ('新表缺少现有文字键，未更新配置：' + ($missing -join ', ')) }
    }
    if ($Preview) { Write-Host "已生成预览，未更新游戏。输出目录：$outputRoot"; return }

    $files = @(
        @{ Source = $generatedJson; Target = $targetJson },
        @{ Source = (Join-Path $codeDir 'Local/Localization.cs'); Target = (Join-Path $projectRoot 'Assets/Hotfix/Config/Generate/Local/Localization.cs') },
        @{ Source = (Join-Path $codeDir 'Local/TbLocalization.cs'); Target = (Join-Path $projectRoot 'Assets/Hotfix/Config/Generate/Local/TbLocalization.cs') }
    )
    $backupDir = Join-Path $outputRoot 'Backup'
    New-Item -ItemType Directory -Force $backupDir | Out-Null
    foreach ($file in $files) {
        if (!(Test-Path -LiteralPath $file.Source) -or !(Test-Path -LiteralPath $file.Target)) { throw "文件不存在：$($file.Source) 或 $($file.Target)" }
        Copy-Item -LiteralPath $file.Target -Destination (Join-Path $backupDir ([IO.Path]::GetFileName($file.Target)))
    }
    try { foreach ($file in $files) { Copy-Item -LiteralPath $file.Source -Destination $file.Target -Force } }
    catch {
        foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $backupDir ([IO.Path]::GetFileName($file.Target))) -Destination $file.Target -Force }
        throw
    }
    Write-Host "已更新多语言 JSON 和生成类。生成结果及修改前备份：$outputRoot"
}
finally { Pop-Location }
