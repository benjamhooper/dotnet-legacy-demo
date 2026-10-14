#Requires -Version 5.1
<#
.SYNOPSIS
    Demo driver for "Real-World .NET in the Enterprise: What Changes When Your
    Codebase Has 10 Years of History".

.DESCRIPTION
    Everything here runs offline in seconds. No database, no cloud subscription,
    no conference wifi, and no NuGet restore (NuGet.config clears every source).

    The four demos, in the order they appear in the talk:

      .\demo.ps1 demo1          MAP: read a decade-old estate in 90 seconds
      .\demo.ps1 demo1-real     ...the same map on a real codebase (names scrubbed)
      .\demo.ps1 demo2          SEAM: a test that cannot exist, then one constructor later, can
      .\demo.ps1 demo2-break    ...and what that test catches on the next "harmless" refactor
      .\demo.ps1 demo3          STRANGLER: shadow the stored procedure, get a NO-GO
      .\demo.ps1 demo3-compat   ...turn every quirk into a decision, get a GO, route 5%
      .\demo.ps1 demo4          TRIAGE + RATCHET: what to leave alone, and a PR that fails
      .\demo.ps1 demo4-fix      ...the same PR through the door

    Between demos:  .\demo.ps1 reset     (restores files AND rebuilds)

    Every demo also writes the same output as a web page, reports/<demo>.html, and
    opens it in the default browser when it finishes. reports/index.html links them
    all. -NoOpen (or $env:LEGACY_NO_OPEN = '1') writes the pages without opening them.

      .\demo.ps1 reports        regenerate every page from scratch, then open the index

.NOTES
    Written for Windows PowerShell 5.1 so it runs on a stock Windows box with
    nothing installed but the .NET SDK. Also runs unchanged in PowerShell 7 on
    macOS and Linux:  pwsh ./demo.ps1 verify
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('help', 'build', 'test', 'verify', 'reset', 'clean', 'transcripts', 'reports',
                 'map', 'seams', 'characterize', 'shadow', 'route', 'triage', 'ratchet',
                 'demo1', 'demo1-real', 'demo2', 'demo2-break', 'demo3', 'demo3-compat', 'demo4', 'demo4-fix')]
    [string]$Task = 'help',

    # Write the HTML report pages but do not open them in a browser.
    [switch]$NoOpen,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$RepoRoot   = Split-Path -Parent $MyInvocation.MyCommand.Path
$LegacyBin  = Join-Path $RepoRoot 'src/LegacyCtl/bin/Debug'
$CharProj   = Join-Path $RepoRoot 'tests/Tallgrass.Billing.Characterization'
$CharBin    = Join-Path $CharProj 'bin/Debug'
$BillingCs  = 'estate/Tallgrass.Billing/BillingManager.cs'
$Reports    = Join-Path $RepoRoot 'reports'
$Report     = $null      # set by Start-Report while a demo is collecting HTML fragments
if ($env:LEGACY_NO_OPEN) { $NoOpen = $true }

# demo1-real: the same map, run against a real codebase that sits NEXT TO this repo
# (never inside it), with names scrubbed by a list that lives beside it. Nothing about
# that codebase is in this repo: its folder name comes from ..\code\real-estate.txt
# (one line, a folder under ..\code) or from $env:LEGACY_REAL_ESTATE.
$TalkRoot   = Split-Path -Parent $RepoRoot
$RealEstate = $env:LEGACY_REAL_ESTATE
if (-not $RealEstate) {
    $pointer = Join-Path $TalkRoot 'code/real-estate.txt'
    $RealEstate = if (Test-Path $pointer) { Join-Path $TalkRoot ('code/' + (Get-Content $pointer -TotalCount 1).Trim()) } else { Join-Path $TalkRoot 'code/(not configured)' }
}
$RealRedact = if ($env:LEGACY_REDACT) { $env:LEGACY_REDACT } else { Join-Path $TalkRoot 'code/redact.txt' }

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

# Escape hatch for a machine without the .NET 10 SDK:
#     $env:LEGACY_TFM = 'net8.0'; .\demo.ps1 verify
$TfmArgs = @()
if ($env:LEGACY_TFM) { $TfmArgs = @("-p:LegacyTargetFramework=$($env:LEGACY_TFM)") }

# ---------------------------------------------------------------------------

function Assert-Dotnet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "The .NET SDK is not on PATH. Install .NET 10: https://dotnet.microsoft.com/download"
    }
}

# Finds a built assembly without knowing which target framework it was built for.
function Get-BuiltDll {
    param([string]$Root, [string]$Name)
    if (-not (Test-Path $Root)) { return $null }
    $found = Get-ChildItem -Path $Root -Filter $Name -Recurse -ErrorAction SilentlyContinue |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1
    if ($null -eq $found) { return $null }
    return $found.FullName
}

function Invoke-Build {
    Assert-Dotnet
    Write-Host ''
    Write-Host 'building...' -ForegroundColor DarkGray
    & dotnet build (Join-Path $RepoRoot 'DotnetLegacyDemo.sln') -v q --nologo @TfmArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "build failed (exit $LASTEXITCODE). If the error is NETSDK1045, this machine has no .NET 10 SDK -- " +
              "either install it, or run: `$env:LEGACY_TFM = 'net8.0'; .\demo.ps1 verify"
    }
}

# Rebuilds only what a demo step changed: the estate's Billing project and the
# characterization harness. About five seconds -- say the next line while it runs.
function Invoke-Rebuild {
    Write-Host 'rebuilding Tallgrass.Billing + characterization...' -ForegroundColor DarkGray
    & dotnet build $CharProj -v q --nologo @TfmArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        & dotnet build $CharProj -v q --nologo @TfmArgs | Out-Host
        throw "rebuild failed (exit $LASTEXITCODE). Run '.\demo.ps1 reset' and try again."
    }
}

function Ensure-Built {
    if ($null -eq (Get-BuiltDll $LegacyBin 'legacy.dll') -or $null -eq (Get-BuiltDll $CharBin 'characterize.dll')) { Invoke-Build }
    $script:LegacyDll = Get-BuiltDll $LegacyBin 'legacy.dll'
    $script:CharDll   = Get-BuiltDll $CharBin 'characterize.dll'
}

# Runs the legacy CLI with output straight to the console.
# Deliberately does NOT pipe output (piping turns colour off) and does NOT throw on
# a non-zero exit: a gate returning 1 is the demo working, not the demo failing.
function Invoke-Legacy {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    Ensure-Built
    if ($null -ne $script:Report -and $Arguments.Count -gt 0 -and
        $Arguments[0] -in @('map', 'seams', 'shadow', 'route', 'triage', 'ratchet', 'diff', 'show', 'step')) {
        $Arguments = @($Arguments) + @('--html', (Get-NextPart $Arguments[0]))
    }
    Push-Location $RepoRoot
    try { & dotnet $script:LegacyDll @Arguments } finally { Pop-Location }
    $script:LegacyExit = $LASTEXITCODE
}

function Invoke-Characterize {
    Ensure-Built
    $script:CharDll = Get-BuiltDll $CharBin 'characterize.dll'
    $charArgs = @()
    if ($null -ne $Rest) { $charArgs += $Rest }
    if ($null -ne $script:Report) { $charArgs += @('--html', (Get-NextPart 'characterize')) }
    Push-Location $RepoRoot
    try { & dotnet $script:CharDll @charArgs } finally { Pop-Location }
    $script:CharExit = $LASTEXITCODE
}

function Write-Beat {
    param([string]$Text)
    Write-Host ''
    Write-Host ">>> $Text" -ForegroundColor Cyan
    if ($null -ne $script:Report) {
        $script:Report.Beat++
        $html = '<div class="beat"><span class="n">{0}</span><span>{1}</span></div>' -f $script:Report.Beat, [System.Net.WebUtility]::HtmlEncode($Text)
        [System.IO.File]::WriteAllText((Get-NextPart 'beat'), $html + "`n")
    }
}

# ---------------------------------------------------------------------------
# HTML reports. A demo calls Start-Report, every legacy/characterize call and every
# beat in between drops a numbered fragment into reports/.parts/<demo>/, and
# Complete-Report stitches them into reports/<demo>.html and opens it.
# ---------------------------------------------------------------------------

function Start-Report {
    param([string]$Name)
    $parts = Join-Path $Reports ".parts/$Name"
    if (Test-Path $parts) { Remove-Item $parts -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $parts | Out-Null
    $script:Report = @{ Name = $Name; Parts = $parts; Seq = 0; Beat = 0 }
}

function Get-NextPart {
    param([string]$Slug)
    $script:Report.Seq++
    return Join-Path $script:Report.Parts ('{0:D2}-{1}.html' -f $script:Report.Seq, $Slug)
}

function Complete-Report {
    param([string]$Label, [string]$Title, [string]$Subtitle, [string]$Result)
    $r = $script:Report
    $script:Report = $null
    $page = Join-Path $Reports "$($r.Name).html"
    $pageArgs = @('page', '--dir', $r.Parts, '--out', $page, '--label', $Label, '--title', $Title, '--subtitle', $Subtitle)
    if ($Result) { $pageArgs += @('--result', $Result) }
    Push-Location $RepoRoot
    # Not piped: piping turns colour off in the CLI, and with it the clickable link.
    try { & dotnet $script:LegacyDll @pageArgs } finally { Pop-Location }
    if (-not $NoOpen) { Open-Page $page }
}

# Prints a file:// URL that Windows Terminal, VS Code and most terminals make
# Ctrl+clickable. Wrapped in an OSC 8 hyperlink where the host understands escapes.
function Write-PageLink {
    param([string]$Label, [string]$Path)
    $full = (Resolve-Path -LiteralPath $Path).ProviderPath
    $url = (New-Object System.Uri($full, [System.UriKind]::Absolute)).AbsoluteUri
    $vt = (-not $env:NO_COLOR) -and $Host.UI.SupportsVirtualTerminal -and -not [Console]::IsOutputRedirected
    if ($vt) {
        $esc = [char]27
        $url = "$esc]8;;$url$esc\$url$esc]8;;$esc\"
    }
    Write-Host ('  {0,-14} ' -f $Label) -NoNewline -ForegroundColor DarkGray
    Write-Host $url
}

function Open-Page {
    param([string]$Path)
    try {
        if ($PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows) { Invoke-Item $Path }
        elseif ($IsMacOS) { & open $Path }
        elseif (Get-Command xdg-open -ErrorAction SilentlyContinue) { & xdg-open $Path *> $null }
    } catch {
        Write-Host "    (could not open a browser; the page is at $Path)" -ForegroundColor DarkGray
    }
}

function Assert-Exit {
    param([string]$What, [int]$Actual, [int]$Expected)
    if ($Actual -eq $Expected) {
        Write-Host ("   [ ok ] {0}  (exit {1})" -f $What, $Actual) -ForegroundColor Green
    } else {
        Write-Host ("   [FAIL] {0}  expected exit {1}, got {2}" -f $What, $Expected, $Actual) -ForegroundColor Red
        $script:VerifyFailed = $true
    }
}

# ---------------------------------------------------------------------------

switch ($Task) {

    'help' {
        Write-Host ''
        Write-Host 'demo.ps1 <task>' -ForegroundColor White
        Write-Host ''
        $tasks = [ordered]@{
            'build'        = 'build the solution'
            'verify'       = 'build + selftest + every demo path and exit code. Must print Ready.'
            'reset'        = 'restore the day-one state and rebuild  (run between demos)'
            'clean'        = 'delete build output and runtime state'
            'transcripts'  = 'regenerate fixtures/transcripts/*.txt (the escape hatch)'
            'reports'      = 'regenerate every reports/<demo>.html page, then open the index'
            ''             = ''
            'map'          = 'legacy map'
            'seams'        = 'legacy seams on BillingManager.CalculateLateFee'
            'characterize' = 'run the late-fee characterization tests'
            'shadow'       = 'legacy shadow (final bill)'
            'route'        = 'legacy route'
            'triage'       = 'legacy triage'
            'ratchet'      = 'legacy ratchet'
            ' '            = ''
            'demo1'        = 'MAP: read the estate before touching it'
            'demo1-real'   = '...the same map on a real codebase next to this repo (names scrubbed)'
            'demo2'        = 'SEAM: blocked test -> one constructor -> 240 cases pinned'
            'demo2-break'  = '...a harmless refactor changes 17 invoices'
            'demo3'        = 'STRANGLER: shadow the stored procedure -> NO-GO, routing refused'
            'demo3-compat' = '...every quirk becomes a decision -> GO, route 5%'
            'demo4'        = 'TRIAGE + RATCHET: leave it alone, and a PR that fails'
            'demo4-fix'    = '...the same PR through the door -> passes'
        }
        foreach ($key in $tasks.Keys) {
            if ($key.Trim() -eq '') { Write-Host ''; continue }
            Write-Host ("  {0,-14} {1}" -f $key, $tasks[$key])
        }
        Write-Host ''
        Write-Host '  Anything else:  dotnet run --project src/LegacyCtl -- --help' -ForegroundColor DarkGray
        Write-Host ''
    }

    'build' { Invoke-Build }

    'test' { Invoke-Characterize; exit $script:CharExit }
    'characterize' { Invoke-Characterize; exit 0 }

    'reset' {
        Invoke-Legacy 'reset'
        Invoke-Rebuild
        Write-Host 'demo-ready.' -ForegroundColor DarkGray
    }

    'clean' {
        foreach ($path in @('.runtime', 'reports', 'src/LegacyCtl/bin', 'src/LegacyCtl/obj',
                            'modern/Tallgrass.FinalBill/bin', 'modern/Tallgrass.FinalBill/obj',
                            'tests/Tallgrass.Billing.Characterization/bin', 'tests/Tallgrass.Billing.Characterization/obj')) {
            $full = Join-Path $RepoRoot $path
            if (Test-Path $full) { Remove-Item $full -Recurse -Force }
        }
        Get-ChildItem (Join-Path $RepoRoot 'estate') -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in @('bin', 'obj') } | Remove-Item -Recurse -Force
        Write-Host 'clean.' -ForegroundColor DarkGray
    }

    'map'     { Invoke-Legacy 'map' @Rest; exit 0 }
    'seams'   { Invoke-Legacy 'seams' $BillingCs '--method' 'CalculateLateFee' @Rest; exit 0 }
    'shadow'  { Invoke-Legacy 'shadow' @Rest; exit 0 }
    'route'   { Invoke-Legacy 'route' @Rest; exit 0 }
    'triage'  { Invoke-Legacy 'triage' @Rest; exit 0 }
    'ratchet' { Invoke-Legacy 'ratchet' @Rest; exit $script:LegacyExit }

    'verify' {
        $script:VerifyFailed = $false
        Invoke-Build
        Invoke-Legacy 'reset'
        Invoke-Rebuild
        Invoke-Legacy 'selftest'
        Assert-Exit 'selftest' $script:LegacyExit 0

        Write-Host ''
        Write-Host 'demo paths:' -ForegroundColor White
        Invoke-Legacy 'map' | Out-Null;                          Assert-Exit 'demo1 map' $script:LegacyExit 0
        Invoke-Legacy 'seams' $BillingCs '--method' 'CalculateLateFee' | Out-Null
                                                                 Assert-Exit 'demo2 seams' $script:LegacyExit 0
        Invoke-Characterize | Out-Null;                          Assert-Exit 'demo2 characterize, day one: blocked' $script:CharExit 2
        Invoke-Legacy 'step' '02-seam' | Out-Null;  Invoke-Rebuild
        Invoke-Characterize | Out-Null;                          Assert-Exit 'demo2 characterize, through the seam: pass' $script:CharExit 0
        Invoke-Legacy 'step' '02-refactor' | Out-Null;  Invoke-Rebuild
        Invoke-Characterize | Out-Null;                          Assert-Exit 'demo2-break characterize: 17 changed' $script:CharExit 1
        Invoke-Legacy 'reset' | Out-Null
        Invoke-Legacy 'shadow' | Out-Null;                       Assert-Exit 'demo3 shadow: NO-GO' $script:LegacyExit 1
        Invoke-Legacy 'route' 'final-bill' '--modern' '5' | Out-Null
                                                                 Assert-Exit 'demo3 route: refused' $script:LegacyExit 1
        Invoke-Legacy 'step' '03-compat' | Out-Null
        Invoke-Legacy 'shadow' | Out-Null;                       Assert-Exit 'demo3-compat shadow: GO' $script:LegacyExit 0
        Invoke-Legacy 'route' 'final-bill' '--modern' '5' | Out-Null
                                                                 Assert-Exit 'demo3-compat route: 5%' $script:LegacyExit 0
        Invoke-Legacy 'triage' | Out-Null;                       Assert-Exit 'demo4 triage' $script:LegacyExit 0
        if ((Test-Path $RealEstate) -and (Test-Path $RealRedact)) {
            Invoke-Legacy 'map' '--estate' $RealEstate '--redact' $RealRedact | Out-Null
                                                                 Assert-Exit 'demo1-real map on the real codebase' $script:LegacyExit 0
        }

        # The HTML side: one fragment, stitched into a page, must come out non-empty.
        $vdir = Join-Path $RepoRoot '.runtime/verify-html'
        Invoke-Legacy 'map' '--html' (Join-Path $vdir '01-map.html') | Out-Null
        Invoke-Legacy 'page' '--dir' $vdir '--out' (Join-Path $RepoRoot '.runtime/verify.html') '--title' 'verify' | Out-Null
        $vpage = Join-Path $RepoRoot '.runtime/verify.html'
        $htmlOk = (Test-Path $vpage) -and ((Get-Content $vpage -Raw) -match 'Who breaks if this changes')
        Assert-Exit 'html report page' ([int](-not $htmlOk)) 0

        Invoke-Legacy 'step' '04-pr' | Out-Null
        Invoke-Legacy 'ratchet' | Out-Null;                      Assert-Exit 'demo4 ratchet: PR fails' $script:LegacyExit 1
        Invoke-Legacy 'step' '04-pr-fixed' | Out-Null
        Invoke-Legacy 'ratchet' | Out-Null;                      Assert-Exit 'demo4-fix ratchet: PR passes' $script:LegacyExit 0

        Invoke-Legacy 'reset' | Out-Null
        Invoke-Rebuild

        Write-Host ''
        if ($script:VerifyFailed) {
            Write-Host 'NOT READY. Fix the failures above before the talk.' -ForegroundColor Red
            exit 1
        }
        Write-Host 'Ready. All four demos will behave.' -ForegroundColor Green
    }

    'transcripts' {
        # Captures every demo with --no-color into fixtures/transcripts/, so that if a
        # laptop dies on stage you can Get-Content the output and keep talking.
        $out = Join-Path $RepoRoot 'fixtures/transcripts'
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        $self = Join-Path $RepoRoot 'demo.ps1'
        $env:NO_COLOR = '1'
        try {
            foreach ($d in @('demo1', 'demo2', 'demo2-break', 'demo3', 'demo3-compat', 'demo4', 'demo4-fix')) {
                if ($d -in @('demo1', 'demo2', 'demo3', 'demo4')) { & $self reset *> $null }
                & $self $d -NoOpen *>&1 | Out-File -FilePath (Join-Path $out "$d.txt") -Encoding utf8
                Write-Host "  wrote fixtures/transcripts/$d.txt" -ForegroundColor DarkGray
            }
        } finally {
            Remove-Item Env:\NO_COLOR -ErrorAction SilentlyContinue
            & $self reset *> $null
        }
    }

    'reports' {
        # Every demo, start to finish, with the console output thrown away: what's left
        # is reports/*.html. Same order and resets as the talk.
        $self = Join-Path $RepoRoot 'demo.ps1'
        try {
            $all = @('demo1', 'demo1-real', 'demo2', 'demo2-break', 'demo3', 'demo3-compat', 'demo4', 'demo4-fix')
            if (-not ((Test-Path $RealEstate) -and (Test-Path $RealRedact))) { $all = $all | Where-Object { $_ -ne 'demo1-real' } }
            foreach ($d in $all) {
                if ($d -in @('demo1', 'demo2', 'demo3', 'demo4')) { & $self reset *> $null }
                & $self $d -NoOpen *> $null
                Write-PageLink $d (Join-Path $Reports "$d.html")
            }
        } finally {
            & $self reset *> $null
        }
        Write-Host ''
        Write-PageLink 'index' (Join-Path $Reports 'index.html')
        Write-Host '  (Ctrl+click a link to open it)' -ForegroundColor DarkGray
        if (-not $NoOpen) { Open-Page (Join-Path $Reports 'index.html') }
    }

    # -----------------------------------------------------------------------
    # the four demos
    # -----------------------------------------------------------------------

    'demo1' {
        Start-Report 'demo1'
        Write-Beat 'Day one. Fourteen projects, nine stored procedures, and a README that says TODO.'
        Invoke-Legacy 'map'
        Write-Beat 'Five questions, answered from files you already have. Nothing to install, nothing to request access to.'
        Complete-Report 'LIVE DEMO 1' 'Map the project' 'Read a decade-old codebase before you touch it.' 'MAPPED:info'
    }

    'demo1-real' {
        if (-not (Test-Path $RealEstate)) {
            Write-Host "No real codebase at $RealEstate (put its folder name in ..\code\real-estate.txt, or set `$env:LEGACY_REAL_ESTATE). Skipping." -ForegroundColor Yellow
            exit 0
        }
        if (-not (Test-Path $RealRedact)) {
            # Fail closed: without the list, product names would go straight to the projector.
            Write-Host "No redaction list at $RealRedact (set `$env:LEGACY_REDACT). Not running." -ForegroundColor Red
            exit 1
        }
        Start-Report 'demo1-real'
        Write-Beat 'Same tool, real code. Nothing in it was written for this talk.'
        Invoke-Legacy 'map' '--estate' $RealEstate '--redact' $RealRedact
        Write-Beat 'Same five questions, same kind of answers. Nobody planned any of it.'
        Complete-Report 'LIVE DEMO 1, PART 2' 'Same tool, real code' 'A real production codebase. Names scrubbed.' 'REAL CODE:info'
    }

    'demo2' {
        Start-Report 'demo2'
        Write-Beat 'The hottest file in the estate. What does CalculateLateFee actually reach for?'
        Invoke-Legacy 'seams' $BillingCs '--method' 'CalculateLateFee'

        Write-Beat 'Attempt 1: pin its behavior with 240 cases recorded from production.'
        Invoke-Characterize

        Write-Beat 'The smallest seam there is: parameterize the constructor. Nothing else moves.'
        Invoke-Legacy 'step' '02-seam'
        Invoke-Legacy 'diff' "steps/00-pristine/$BillingCs" $BillingCs
        Invoke-Rebuild

        Write-Beat 'Attempt 2. Same cases, through the seam.'
        Invoke-Characterize
        Write-Beat 'Behavior pinned. Not "correct", pinned. Now we are allowed to change it.'
        $result = if ($script:CharExit -eq 0) { 'PASS 240 of 240 pinned:good' } else { "characterize exit $($script:CharExit):bad" }
        Complete-Report 'LIVE DEMO 2' 'The seam' 'A test that cannot exist, then one constructor later, can.' $result
    }

    'demo2-break' {
        Start-Report 'demo2-break'
        Write-Beat 'A teammate tidies up the rounding. Two lines. Code review would approve it.'
        Invoke-Legacy 'step' '02-refactor'
        Invoke-Legacy 'diff' "steps/02-seam/$BillingCs" $BillingCs
        Invoke-Rebuild
        Invoke-Characterize
        Write-Beat 'The test has no opinion about what is right. It knows what customers were billed.'
        $result = if ($script:CharExit -eq 1) { 'FAIL caught by the characterization:bad' } else { "characterize exit $($script:CharExit):warn" }
        Complete-Report 'LIVE DEMO 2, PART 2' 'The harmless refactor' 'Two lines of tidy-up, checked against 240 recorded invoices.' $result
        Write-Host "    Run '.\demo.ps1 reset' before the next demo." -ForegroundColor DarkGray
    }

    'demo3' {
        Start-Report 'demo3'
        Write-Beat 'dbo.usp_ProrateFinalBill: the final-bill math, in T-SQL, called from three projects.'
        Invoke-Legacy 'show' 'estate/Database/Procedures/dbo.usp_ProrateFinalBill.sql' '--from' '23' '--to' '30' '--highlight' '24,29,30' '--title' 'The final bill, in T-SQL'
        Write-Beat 'Step one of the strangler is a door, not a rewrite. Every caller now goes through the router:'
        Invoke-Legacy 'route'
        Write-Beat '30 days of production traffic, replayed through the new code written from the tariff:'
        Invoke-Legacy 'shadow'
        Write-Beat 'Can we send 5% of customers to it?'
        Invoke-Legacy 'route' 'final-bill' '--modern' '5'
        $result = if ($script:LegacyExit -eq 1) { 'NO-GO, routing refused:bad' } else { "route exit $($script:LegacyExit):warn" }
        Complete-Report 'LIVE DEMO 3' 'Strangle the stored procedure' 'A door first, then a shadow. The shadow says NO-GO.' $result
    }

    'demo3-compat' {
        Start-Report 'demo3-compat'
        Write-Beat 'Every quirk becomes a named decision, with a ticket. One difference is accepted, with an owner and an expiry.'
        Invoke-Legacy 'step' '03-compat'
        Invoke-Legacy 'diff' 'steps/00-pristine/modern/final-bill.policy.json' 'modern/final-bill.policy.json' '--context' '1'
        Invoke-Legacy 'shadow'
        Invoke-Legacy 'route' 'final-bill' '--modern' '5'
        Write-Beat 'Five percent, sticky per account. The other ninety-five keep shadowing. Next week, twenty-five.'
        $result = if ($script:LegacyExit -eq 0) { 'GO, 5% routed:good' } else { "route exit $($script:LegacyExit):bad" }
        Complete-Report 'LIVE DEMO 3, PART 2' 'Every quirk becomes a decision' 'A compatibility policy, a GO, and five percent of customers.' $result
    }

    'demo4' {
        Start-Report 'demo4'
        Write-Beat 'Where does the modernization budget go? Ugliness is not one of the inputs.'
        Invoke-Legacy 'triage'
        Write-Beat 'Leave alone does not mean unguarded. A teammate opens a PR: "just a report."'
        Invoke-Legacy 'step' '04-pr'
        Invoke-Legacy 'ratchet'
        $result = if ($script:LegacyExit -eq 1) { 'PR FAILS the ratchet:bad' } else { "ratchet exit $($script:LegacyExit):warn" }
        Complete-Report 'LIVE DEMO 4' 'Triage and the ratchet' 'What to leave alone, and a pull request that does not get to make it worse.' $result
    }

    'demo4-fix' {
        Start-Report 'demo4-fix'
        Write-Beat 'Same PR, second push. The dependencies come in through the constructor.'
        Invoke-Legacy 'step' '04-pr-fixed'
        Invoke-Legacy 'ratchet'
        Write-Beat 'You will not remove 124 DateTime.Now calls this quarter. You can make sure there is never a 125th.'
        $result = if ($script:LegacyExit -eq 0) { 'PR PASSES the ratchet:good' } else { "ratchet exit $($script:LegacyExit):bad" }
        Complete-Report 'LIVE DEMO 4, PART 2' 'The same PR, through the door' 'Dependencies come in through the constructor. The ratchet passes.' $result
    }
}
