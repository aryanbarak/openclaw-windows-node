#requires -Version 5.1
<#
.SYNOPSIS
    Isolated cleanup regressions. No installed apps, user profiles, WSL, or real processes are changed.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'clean-uninstall.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
# Load function definitions only. Never run the script's real inventory/entry point.
foreach ($definition in $ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.FunctionDefinitionAst] }) {
    Invoke-Expression $definition.Extent.Text
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ("openclaw-clean-tests-" + [guid]::NewGuid().ToString('N'))
$passed = 0
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-Throws([scriptblock]$Action, [string]$Pattern) {
    try { & $Action } catch {
        if ($_.Exception.Message -notmatch $Pattern) { throw "Unexpected error: $_" }
        return
    }
    throw "Expected rejection matching: $Pattern"
}
function Test-Case([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:passed++
    Write-Host "PASS: $Name"
}
$oldEnvironment = @{}
$environmentNames = @('APPDATA','LOCALAPPDATA','OPENCLAW_TRAY_DATA_DIR','OPENCLAW_TRAY_APPDATA_DIR',
    'OPENCLAW_TRAY_LOCALAPPDATA_DIR','OPENCLAW_TRAY_LOCAL_DATA_DIR','OPENCLAW_STATE_DIR','OPENCLAW_CONFIG_PATH')
foreach ($name in $environmentNames) { $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    New-Item -ItemType Directory -Path $fixture | Out-Null
    Test-Case 'All expands only known opt-ins and never implies destructive confirmation' {
        $attributes = ($ast.ParamBlock.Attributes | ForEach-Object { $_.Extent.Text }) -join "`n"
        $entryPoint = [scriptblock]::Create(
            $attributes + "`n" + $ast.ParamBlock.Extent.Text + "`n" + $ast.EndBlock.Statements[-1].Extent.Text)
        function Invoke-OpenClawClean {
            param([bool]$Apply, [bool]$Dev, [bool]$Models, [bool]$Wsl, [string[]]$Extra, [string]$Reports)
            [pscustomobject]@{ Apply = $Apply; Dev = $Dev; Models = $Models; Wsl = $Wsl; Extra = $Extra; Reports = $Reports; WhatIf = $WhatIfPreference }
        }
        $defaults = & $entryPoint
        Assert-True (-not $defaults.Apply -and -not $defaults.Dev -and -not $defaults.Models -and -not $defaults.Wsl) 'Default scope changed.'
        $all = & $entryPoint -all
        Assert-True (-not $all.Apply -and $all.Dev -and $all.Models -and $all.Wsl) 'All did not expand safely.'
        Assert-True (@($all.Extra).Count -eq 0) 'All invented extra profile paths.'
        $individual = & $entryPoint -All:$false -RemoveCachedModels
        Assert-True (-not $individual.Apply -and -not $individual.Dev -and $individual.Models -and -not $individual.Wsl) 'Individual flags stopped working.'
        $extras = @("$fixture\extra-one", "$fixture\extra-two")
        $confirmed = & $entryPoint -All -ConfirmDestructive -AdditionalProfilePath $extras -ReportDirectory "$fixture\reports"
        Assert-True ($confirmed.Apply -and $confirmed.Dev -and $confirmed.Models -and $confirmed.Wsl) 'Confirmed All lost an option.'
        Assert-True (($confirmed.Extra -join '|') -eq ($extras -join '|') -and $confirmed.Reports -eq "$fixture\reports") 'Explicit paths were not preserved.'
        $whatIf = & $entryPoint -All -ConfirmDestructive -WhatIf
        Assert-True ($whatIf.WhatIf) 'All did not propagate WhatIf.'
    }
    Test-Case 'root, ancestor, system, and session guards' {
        foreach ($path in @($env:USERPROFILE, $env:TEMP, 'C:\', "$env:USERPROFILE\.copilot",
            "$env:USERPROFILE\.copilot\session-state\11111111-1111-1111-1111-111111111111",
            "$env:USERPROFILE\.cache\huggingface\hub", "$env:WINDIR\System32")) {
            Assert-Throws { Assert-CleanPath $path } 'Refusing'
        }
        foreach ($path in @('', '.', 'C:relative', '\\server\share', "$fixture\*", "$fixture\x:stream")) {
            Assert-Throws { Assert-CleanPath $path } 'absolute local literal'
        }
        foreach ($path in @("$env:TEMP.", "$fixture\..", "$fixture\NUL.txt", "$fixture\folder ", "$fixture\USERNA~1")) {
            Assert-Throws { Assert-CleanPath $path } 'ambiguous Windows'
        }
        Assert-Throws { Assert-CleanPath (Join-Path (Split-Path $env:USERPROFILE) 'UnrelatedUser\Documents') } "another user's"
    }
    Test-Case 'tree inventory includes hidden files but refuses source checkout' {
        $tree = Join-Path $fixture 'tree'
        New-Item -ItemType Directory -Path $tree | Out-Null
        Set-Content -LiteralPath "$tree\one.txt" -Value 'safe'
        Assert-True (@(Get-CleanTree $tree).Count -eq 2) 'Tree inventory omitted a file.'
        Set-Content -LiteralPath "$tree\.git" -Value 'gitdir: elsewhere'
        Assert-Throws { @(Get-CleanTree $tree) } 'source checkout'
    }
    Test-Case 'junctions and ancestor junctions are rejected without traversing' {
        $outside = Join-Path $fixture 'outside'
        $inside = Join-Path $fixture 'inside'
        New-Item -ItemType Directory -Path $outside,$inside | Out-Null
        Set-Content -LiteralPath "$outside\keep.txt" -Value 'preserve'
        $junction = Join-Path $inside 'link'
        New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
        try {
            Assert-Throws { @(Get-CleanTree $inside) } 'reparse'
            Assert-Throws { Assert-CleanPath "$junction\keep.txt" } 'reparse'
            Assert-True (Test-Path -LiteralPath "$outside\keep.txt") 'Junction destination changed.'
        } finally {
            # Delete the junction itself, never its target.
            [IO.Directory]::Delete($junction)
        }
    }
    Test-Case 'schema 4 and 5 select only exact receipt-backed cache files' {
        $cache = Join-Path $fixture 'cache'
        $snapshot = Join-Path $cache ('models--test--model\snapshots\' + ('a' * 40))
        $localAi = Join-Path $fixture 'profile\LocalAI'
        New-Item -ItemType Directory -Path $snapshot,$localAi -Force | Out-Null
        Set-Content -LiteralPath "$snapshot\model.gguf" -Value 'model'
        Set-Content -LiteralPath "$snapshot\draft.gguf" -Value 'draft'
        $asset = [ordered]@{ FileName = 'model.gguf'; SizeBytes = (Get-Item "$snapshot\model.gguf").Length; Sha256 = (Get-FileHash "$snapshot\model.gguf").Hash }
        $manifest = [ordered]@{ SchemaVersion = 4; ModelCacheRoot = $cache; CachedModelPath = "$snapshot\model.gguf"; ModelAsset = $asset }
        $receiptFile = "$localAi\state.json"
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receiptFile
        Assert-True (@(Get-CleanModels @((Get-Item $receiptFile))).Count -eq 1) 'Schema 4 lost model.'
        $manifest.SchemaVersion = 5
        $manifest.AdditionalModelPaths = @("$snapshot\draft.gguf")
        $manifest.AdditionalModelAssets = @([ordered]@{ FileName = 'draft.gguf'; SizeBytes = (Get-Item "$snapshot\draft.gguf").Length; Sha256 = (Get-FileHash "$snapshot\draft.gguf").Hash })
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receiptFile
        Assert-True (@(Get-CleanModels @((Get-Item $receiptFile))).Count -eq 2) 'Schema 5 lost additional asset.'
        $manifest.CachedModelPath = "$fixture\unrelated.gguf"
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receiptFile
        Assert-Throws { Get-CleanModels @((Get-Item $receiptFile)) } 'exact Hugging Face'
        $manifest.CachedModelPath = "$snapshot\model.gguf"
        $manifest.ModelAsset.SizeBytes = 1
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receiptFile
        Assert-Throws { Get-CleanModels @((Get-Item $receiptFile)) } 'size/type mismatch'
    }
    Test-Case 'PID reuse never stops a replacement process' {
        function Get-CimInstance { [pscustomobject]@{ ProcessId = 42; ExecutablePath = 'C:\other.exe'; CreationDate = 'new' } }
        function Stop-Process { throw 'TEST FAILURE: a real stop was attempted' }
        Assert-Throws { Stop-CleanProcess ([pscustomobject]@{ ProcessId = 42; ExecutablePath = 'C:\owned.exe'; CreationDate = 'old' }) } 'changed identity'
    }
    Test-Case 'native capture preserves nonzero exit codes and bounds waits' {
        $powerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $result = Invoke-CleanNativeCommand $powerShell '-NoProfile -Command "[Console]::Out.WriteLine(''out''); [Console]::Error.WriteLine(''err''); exit 7"'
        Assert-True ($result.ExitCode -eq 7 -and $result.Stdout.Trim() -eq 'out' -and $result.Stderr.Trim() -eq 'err') 'Native output/exit code was lost.'
        Assert-Throws { Invoke-CleanNativeCommand $powerShell '-NoProfile -Command "Start-Sleep -Seconds 30"' 200 } 'timed out'
    }

    # All OS-facing operations are mocked for lifecycle tests.
    foreach ($name in $environmentNames | Where-Object { $_ -notin @('APPDATA','LOCALAPPDATA') }) {
        [Environment]::SetEnvironmentVariable($name, $null)
    }
    $env:APPDATA = Join-Path $fixture 'roaming'
    $env:LOCALAPPDATA = Join-Path $fixture 'local'
    New-Item -ItemType Directory -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
    $script:events = [Collections.Generic.List[string]]::new()
    $script:registered = $true
    $script:teardownFails = $false
    $script:gateway = [pscustomobject]@{
        Name = 'OpenClawFoundation.OpenClawGateway'
        PackageFullName = 'OpenClawFoundation.OpenClawGateway_test'
        PackageFamilyName = 'OpenClawFoundation.OpenClawGateway_123456789abcd'
        InstallLocation = "$fixture\installed"
    }
    $alias = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\$($gateway.PackageFamilyName)\clawctl.exe"
    New-Item -ItemType Directory -Path (Split-Path $alias) -Force | Out-Null
    Set-Content -LiteralPath $alias -Value 'not executable'
    Test-Case 'native teardown checks structured result as well as process exit code' {
        $reports = Join-Path $fixture 'native-contract'
        New-Item -ItemType Directory -Path $reports | Out-Null
        $script:nativeExit = 0
        $script:nativeJson = '{"ok":true,"command":"teardown","gateway":{"state":"records-removed"},"session":{"state":"removed"}}'
        function Invoke-CleanNativeCommand {
            param($FilePath, $Arguments)
            Assert-True ($FilePath -eq $alias) 'Teardown did not use the package-qualified alias.'
            Assert-True ($Arguments -eq 'teardown --force --json') 'Wrong native teardown arguments.'
            return [pscustomobject]@{ ExitCode = $script:nativeExit; Stdout = $script:nativeJson; Stderr = '' }
        }
        Invoke-CleanTeardown $gateway $reports
        foreach ($json in @('{"ok":false}', '{"ok":"true"}',
            '{"ok":true,"command":"teardown","gateway":{"state":"running"},"session":{"state":"removed"}}',
            '{"ok":true,"command":"setup","gateway":{"state":"records-removed"},"session":{"state":"removed"}}')) {
            $script:nativeJson = $json
            Assert-Throws { Invoke-CleanTeardown $gateway $reports } 'did not confirm success'
        }
        $script:nativeExit = 1
        $script:nativeJson = '{"ok":true,"command":"teardown","gateway":{"state":"records-removed"},"session":{"state":"not-configured"}}'
        Assert-Throws { Invoke-CleanTeardown $gateway $reports } 'did not confirm success'
    }
    $script:distroInventory = @('UnrelatedDistro')
    function Get-CleanPackages { if ($script:registered) { $script:gateway } }
    function Get-CleanWin32 { }
    function Get-CleanDistros { $script:distroInventory }
    function wsl.exe {
        Assert-True ($args.Count -eq 2 -and $args[0] -eq '--unregister') 'Unexpected WSL command.'
        $script:events.Add("unregister:$($args[1])")
        $target = $args[1]
        $script:distroInventory = @($script:distroInventory | Where-Object { $_ -ne $target })
        $global:LASTEXITCODE = 0
    }
    function Get-CleanProcesses { }
    function Get-CleanStartup { }
    function Invoke-CleanTeardown {
        $script:events.Add('teardown')
        if ($script:teardownFails) { throw 'TEST native teardown failed' }
    }
    function Remove-AppxPackage {
        $script:events.Add('package')
        Assert-True ($script:events[0] -eq 'teardown') 'Package removed before native teardown.'
        $script:registered = $false
    }
    function Start-Transcript { }
    function Stop-Transcript { }
    function New-Profile {
        New-Item -ItemType Directory -Path "$env:APPDATA\OpenClawTray" -Force | Out-Null
        Set-Content -LiteralPath "$env:APPDATA\OpenClawTray\settings.json" -Value '{}'
    }
    New-Profile
    Test-Case 'default preview and WhatIf never mutate targets or create reports' {
        $report = Join-Path $fixture 'dry-reports'
        Invoke-OpenClawClean -Apply $false -Dev $false -Models $false -Wsl $false -Extra @() -Reports $report
        Invoke-OpenClawClean -Apply $true -Dev $false -Models $false -Wsl $false -Extra @() -Reports $report -WhatIf
        Assert-True ($events.Count -eq 0) 'Preview invoked a mutator.'
        Assert-True (-not (Test-Path $report)) 'Preview created reports.'
        Assert-True (Test-Path "$env:APPDATA\OpenClawTray\settings.json") 'Preview removed state.'
    }
    Test-Case 'path overrides block cleanup before native mutation' {
        $env:OPENCLAW_STATE_DIR = $fixture
        try {
            Assert-Throws { Invoke-OpenClawClean -Apply $true -Dev $false -Models $false -Wsl $false -Extra @() } 'Clear path overrides'
        } finally { $env:OPENCLAW_STATE_DIR = $null }
        Assert-True ($events.Count -eq 0) 'Override rejection invoked a mutator.'
    }
    Test-Case 'failed native teardown preserves packages and profile' {
        $script:teardownFails = $true
        Assert-Throws { Invoke-OpenClawClean -Apply $true -Dev $false -Models $false -Wsl $false -Extra @() -Reports "$fixture\failed-reports" } 'TEST native teardown failed'
        Assert-True ($script:registered) 'Failed teardown removed package.'
        Assert-True (Test-Path "$env:APPDATA\OpenClawTray\settings.json") 'Failed teardown removed profile.'
        $script:teardownFails = $false
        $events.Clear()
    }
    Test-Case 'successful cleanup orders teardown before package/state removal and retains unrelated data' {
        Invoke-OpenClawClean -Apply $true -Dev $false -Models $false -Wsl $false -Extra @() -Reports "$fixture\success-reports"
        Assert-True (($events -join ',') -eq 'teardown,package') 'Unexpected lifecycle order.'
        Assert-True (-not (Test-Path "$env:APPDATA\OpenClawTray")) 'Profile remains.'
        Assert-True (Test-Path "$fixture\outside\keep.txt") 'Unrelated data was deleted.'
        Assert-True (Test-Path "$fixture\success-reports\plan.json") 'Plan not persisted.'
    }
    Test-Case 'repeat cleanup is idempotent on absent targets' {
        $events.Clear()
        Invoke-OpenClawClean -Apply $true -Dev $false -Models $false -Wsl $false -Extra @() -Reports "$fixture\repeat-reports"
        Assert-True ($events.Count -eq 0) 'Absent package was acted on.'
    }
    Test-Case 'WSL deletion is opt-in and exact, with dev distro preserved by default' {
        $script:distroInventory = @('OpenClawGateway', 'OpenClawGateway-Dev', 'UnrelatedDistro', 'OpenClawGateway-extra')
        Invoke-OpenClawClean -Apply $true -Dev $false -Models $false -Wsl $true -Extra @() -Reports "$fixture\wsl-reports"
        Assert-True (($events -join ',') -eq 'unregister:OpenClawGateway') 'WSL scope widened.'
        Assert-True (($script:distroInventory -join ',') -eq 'OpenClawGateway-Dev,UnrelatedDistro,OpenClawGateway-extra') 'Preserved WSL registrations changed.'
    }
    Test-Case 'Inno removing an approved WSL distro does not abort residual cleanup' {
        $events.Clear()
        New-Profile
        $script:win32Registered = $true
        $script:distroInventory = @('OpenClawGateway', 'UnrelatedDistro')
        function Get-CleanWin32 {
            if ($script:win32Registered) {
                [pscustomobject]@{ Name = 'OpenClaw Companion'; Install = "$fixture\inno"; Exe = "$fixture\inno\unins000.exe" }
            }
        }
        function Invoke-CleanWin32 {
            $script:events.Add('inno')
            $script:win32Registered = $false
            $script:distroInventory = @('UnrelatedDistro')
        }
        Invoke-OpenClawClean -Apply $true -Dev $false -Models $false -Wsl $true -Extra @() -Reports "$fixture\inno-wsl-reports"
        Assert-True (($events -join ',') -eq 'inno') 'An already-removed distro was unregistered again.'
        Assert-True (-not (Test-Path "$env:APPDATA\OpenClawTray")) 'Inno WSL removal prevented profile cleanup.'
    }
    Test-Case 'remaining package data blocks deletion instead of claiming a clean device' {
        $events.Clear()
        New-Profile
        $script:registered = $true
        $leftover = Join-Path $env:LOCALAPPDATA "Packages\$($gateway.PackageFamilyName)\LocalState"
        New-Item -ItemType Directory -Path $leftover -Force | Out-Null
        Set-Content -LiteralPath "$leftover\remaining.txt" -Value 'retained package data'
        Assert-Throws { Invoke-OpenClawClean -Apply $true -Dev $false -Models $false -Wsl $false -Extra @() -Reports "$fixture\package-remnant-reports" } 'Package data remains'
        Assert-True (Test-Path "$env:APPDATA\OpenClawTray\settings.json") 'Profiles removed despite incomplete package cleanup.'
        Assert-True (Test-Path "$leftover\remaining.txt") 'Package-managed data was deleted manually.'
    }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name]) }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
Write-Host "Clean uninstall regressions passed: $passed"
