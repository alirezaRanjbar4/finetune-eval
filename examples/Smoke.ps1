param([string]$ExpectedCommit,[string]$PackageDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$previousCache=$env:NUGET_PACKAGES
Push-Location $root
try {
    if(-not $ExpectedCommit){$ExpectedCommit=git rev-parse HEAD;if($LASTEXITCODE -ne 0){throw 'Cannot determine expected Git commit.'}}
    if(-not $PackageDirectory){$PackageDirectory=Join-Path $root 'artifacts'}
    $feed=(Resolve-Path -LiteralPath $PackageDirectory).Path
    $package=Join-Path $feed 'FineTuneEval.Tool.0.3.1.nupkg'
    $packageHash=(Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    $runDirectory=Join-Path $root ('artifacts/smoke-'+[guid]::NewGuid().ToString('N'))
    $fixtureDirectory=Join-Path $runDirectory 'examples'
    New-Item -ItemType Directory -Path $fixtureDirectory -Force|Out-Null
    $archive=[IO.Compression.ZipFile]::OpenRead($package)
    try {
        foreach($name in @('README.md','LICENSE','CONTRIBUTING.md','ROADMAP.md','CHANGELOG.md','docs/DESIGN.md','docs/TROUBLESHOOTING.md','examples/README.md','examples/Smoke.ps1','.github/workflows/ci.yml')) {
            if(-not $archive.GetEntry($name)){throw ('Missing packaged document: '+$name)}
        }
        $manifests=@($archive.Entries|Where-Object {$_.FullName -notmatch '/' -and $_.FullName.EndsWith('.nuspec')})
        if($manifests.Count -ne 1){throw 'Expected one root manifest.'}
        $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null;$settings.MaxCharactersInDocument=1048576
        $manifestStream=$manifests[0].Open()
        $reader=[Xml.XmlReader]::Create($manifestStream,$settings)
        try {$manifest=[Xml.XmlDocument]::new();$manifest.XmlResolver=$null;$manifest.Load($reader)} finally {$reader.Dispose();$manifestStream.Dispose()}
        $metadata=$manifest.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
        if($metadata.SelectSingleNode('*[local-name()="id"]').InnerText -ne 'FineTuneEval.Tool' -or $metadata.SelectSingleNode('*[local-name()="version"]').InnerText -ne '0.3.1' -or $metadata.SelectSingleNode('*[local-name()="repository"]').GetAttribute('commit') -ne $ExpectedCommit){throw 'Final package identity/version/commit mismatch.'}
        foreach($name in @('cases.jsonl','baseline.jsonl','candidate.jsonl','slice-gates.json')) {
            $entry=$archive.GetEntry('examples/'+$name)
            if(-not $entry -or $entry.Length -gt 1048576){throw 'Missing/oversized packaged fixture.'}
            $stream=$entry.Open();$bytes=[IO.MemoryStream]::new()
            try {$stream.CopyTo($bytes);$payload=$bytes.ToArray()} finally {$stream.Dispose();$bytes.Dispose()}
            $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($payload))
            if($digest -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $name) -Algorithm SHA256).Hash){throw 'Packaged fixture bytes differ from source.'}
            [IO.File]::WriteAllBytes((Join-Path $fixtureDirectory $name),$payload)
        }
        $assembly=$archive.GetEntry('tools/net10.0/any/FineTuneEval.dll')
        if(-not $assembly){throw 'Missing tool assembly.'}
        $stream=$assembly.Open()
        try {$assemblyHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))} finally {$stream.Dispose()}
    } finally {$archive.Dispose()}
    $config=Join-Path $runDirectory 'NuGet.Config'
    $escapedFeed=[Security.SecurityElement]::Escape($feed)
    [IO.File]::WriteAllText($config,('<configuration><packageSources><clear/><add key="local" value="'+$escapedFeed+'"/></packageSources></configuration>'))
    $env:NUGET_PACKAGES=Join-Path $runDirectory 'packages'
    $toolDirectory=Join-Path $runDirectory 'tool'
    dotnet tool install FineTuneEval.Tool --version 0.3.1 --tool-path $toolDirectory --configfile $config --no-cache
    if($LASTEXITCODE -ne 0){throw 'Fresh local-only installation failed.'}
    $tool=Join-Path $toolDirectory 'finetune-eval'
    if((& $tool --version) -ne '0.3.1' -or $LASTEXITCODE -ne 0){throw 'Installed version mismatch.'}
    $installed=@(Get-ChildItem -LiteralPath $toolDirectory -Recurse -Filter FineTuneEval.dll)
    if($installed.Count -ne 1 -or (Get-FileHash -LiteralPath $installed[0].FullName -Algorithm SHA256).Hash -ne $assemblyHash){throw 'Installed assembly differs from package payload.'}
    $inputs=@('cases.jsonl','baseline.jsonl','candidate.jsonl','slice-gates.json')|ForEach-Object {Join-Path $fixtureDirectory $_}
    $before=@($inputs|ForEach-Object {(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash})
    $arguments=@('compare','--cases',$inputs[0],'--baseline',$inputs[1],'--candidate',$inputs[2],'--slice-gates',$inputs[3],'--min-exact','1','--min-f1','1','--max-drop','0')
    $standard=& $tool @arguments|ConvertFrom-Json
    if($LASTEXITCODE -ne 0 -or $standard.cases -ne 3 -or $standard.candidate.exactMatch -ne 1 -or $standard.candidate.tokenF1 -ne 1 -or @($standard.supportWarnings).Count -ne 0){throw 'Passing example failed.'}
    $warned=& $tool @arguments --min-slice-support 2|ConvertFrom-Json
    if($LASTEXITCODE -ne 0 -or ($warned.supportWarnings.tag -join ',') -ne 'fa,support' -or $warned.minimumSliceSupport -ne 2){throw 'Support boundary failed.'}
    foreach($field in @('baseline','candidate','delta','results','slices','passed')){if(($standard.$field|ConvertTo-Json -Depth 20 -Compress) -ne ($warned.$field|ConvertTo-Json -Depth 20 -Compress)){throw 'Support warning changed scoring or gates.'}}
    $negative=Join-Path $runDirectory 'empty-predictions.jsonl'
    Get-Content -LiteralPath $inputs[0]|ForEach-Object {$row=$_|ConvertFrom-Json;@{id=$row.id;output=''}|ConvertTo-Json -Compress}|Set-Content -LiteralPath $negative -Encoding utf8
    $failed=& $tool compare --cases $inputs[0] --baseline $inputs[1] --candidate $negative --min-exact 1 --min-slice-support 2|ConvertFrom-Json
    if($LASTEXITCODE -ne 1 -or $failed.passed){throw 'Failing gate did not exit 1.'}
    $invalid=& $tool @arguments --min-slice-support -1 2> (Join-Path $runDirectory 'invalid.stderr.txt')
    if($LASTEXITCODE -ne 2 -or $invalid){throw 'Invalid option did not exit 2 without JSON.'}
    $after=@($inputs|ForEach-Object {(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash})
    if(($before -join ',') -ne ($after -join ',') -or (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $packageHash){throw 'Source package/examples changed.'}
    $proof=@{version='0.3.1';commit=$ExpectedCommit;packageSha256=$packageHash.ToLowerInvariant();installedAssemblyMatchesPayload=$true;exampleCases=3;passingFailingInvalidExits='0/1/2 verified';supportBoundary='fa/support warn; en does not';scoresAndGates='unchanged';inputsAndPackage='unchanged';localOnlyFreshCache=$true;evidenceRoot=$runDirectory}
    $proof|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $runDirectory 'verification.json') -Encoding utf8
    Write-Output ('Installed-package smoke passed; evidence: '+$runDirectory)
    $global:LASTEXITCODE=0
} finally {$env:NUGET_PACKAGES=$previousCache;Pop-Location}
