# dotnet-legacy-demo

Companion repository for **"Real-World .NET in the Enterprise: What Changes When
Your Codebase Has 10 Years of History."**

A fictional ten-year-old .NET estate (`estate/`), and one small CLI, `legacy`,
that does the four things this talk argues for, in order:

| | Question it answers | Command | Demo |
|---|---|---|---|
| **Map** | What depends on what, what is too big, what logic is in SQL, where does change happen, where do I start? | `legacy map` | 1 |
| **Seam** | Why can't this method be tested, and what is the smallest change that fixes that? | `legacy seams` + `characterize` | 2 |
| **Strangle** | Does the replacement behave like production, and can every difference be explained? | `legacy shadow` / `legacy route` | 3 |
| **Triage + ratchet** | Where does the budget go, what gets left alone, and how do we stop the legacy growing? | `legacy triage` / `legacy ratchet` | 4 |

---

## Quick start

**Requirements: the .NET 10 SDK. That is the entire list.**

No database, no Docker, no cloud account, no NuGet feed: `NuGet.config` clears
every source, so restore is offline.

### Windows (PowerShell)

```powershell
.\demo.ps1 verify      # build, 32 self-checks, every demo path and exit code. Prints "Ready."
.\demo.ps1 demo1       # map the estate
.\demo.ps1 help        # everything else
```

Windows PowerShell 5.1 is fine. If script execution is blocked:
`powershell -ExecutionPolicy Bypass -File .\demo.ps1 verify`

**Older SDK?** `$env:LEGACY_TFM = 'net8.0'; .\demo.ps1 verify`

### macOS / Linux

```bash
pwsh ./demo.ps1 verify                 # PowerShell 7: brew install powershell
dotnet run --project src/LegacyCtl -- map
```

---

## The four demos

```powershell
.\demo.ps1 demo1          # MAP: fourteen projects, nine procedures, five questions
.\demo.ps1 demo1-real     #   the same map on a real codebase next to this repo, names scrubbed
.\demo.ps1 demo2          # SEAM: BLOCKED 240/240 -> one constructor -> PASS 240/240
.\demo.ps1 demo2-break    #   a "harmless" rounding tidy-up changes 17 invoices
.\demo.ps1 reset
.\demo.ps1 demo3          # STRANGLER: shadow says 926 of 1,000 differ -> NO-GO, route refused
.\demo.ps1 demo3-compat   #   every quirk becomes a named decision -> GO, route 5%
.\demo.ps1 reset
.\demo.ps1 demo4          # TRIAGE + RATCHET: leave the ugliest file alone; a PR fails the ratchet
.\demo.ps1 demo4-fix      #   the same PR through the constructor -> passes
.\demo.ps1 reset
```

Every demo also writes its output as a web page, `reports/<demo>.html` (tiles,
bars, a dependency graph, highlighted code, PASS / FAIL / GO / NO-GO banners),
and opens it in the default browser when it finishes; `reports/index.html` links
them all. The terminal prints a Ctrl+clickable `file:///` link to each page as it
is built. Add `-NoOpen` to write the pages without opening them, or run
`.\demo.ps1 reports` to regenerate all seven at once. The pages are single
self-contained HTML files: no JavaScript, no CDN, they open from disk offline.

Under the hood every `legacy` report command takes `--html <file>` and writes the
same report as an HTML fragment (`src/LegacyCtl/Views.cs`, styles in
`src/Shared/HtmlReport.cs`), and `legacy page` stitches a demo's fragments into
one page:

```bash
dotnet run --project src/LegacyCtl -- map --html map.html
dotnet run --project src/LegacyCtl -- page --dir reports/.parts/demo1 --out reports/demo1.html --title "Map the project"
```

Every live change is a pre-written step in `steps/`, applied by `legacy step`;
nothing is typed on stage. `.\demo.ps1 transcripts` captures the output of every
demo into `fixtures/transcripts/`, in case a laptop dies mid-talk.

---

## Layout

```
estate/                     the fictional 2011 system (see estate/README.md)
  Tallgrass.Billing/        BillingManager.cs: the god class demo 2 seams
  Database/Procedures/      dbo.usp_ProrateFinalBill.sql: the logic demo 3 strangles
  .history/                 churn and "signals": what git log and people tell you
modern/
  Tallgrass.FinalBill/      the replacement + FinalBillRouter (the strangler door)
  final-bill.policy.json    every legacy quirk as a named option
  routes.json               legacy | shadow | modern, per capability
src/LegacyCtl/              the `legacy` CLI (Views.cs: the HTML side of every report)
src/Shared/                 console colours + the HTML report kit
tests/Tallgrass.Billing.Characterization/
                            240 recorded cases for CalculateLateFee
steps/                      the demo's file changes, applied by `legacy step`
fixtures/                   golden cases and recorded shadow traffic
reports/                    generated web page per demo (git-ignored)
ratchet.baseline.json       seam-blocker counts; may go down, never up
dotnet-enterprise-talk-branded.pptx   the slides
```

## Using it on your own code

`legacy map` reads any tree of `.csproj`, `.cs`, `.sln` and `.sql` files as text,
and takes change history straight from git (renames followed):

```powershell
dotnet run --project src/LegacyCtl -- map --estate C:\src\YourSolution --html map.html
```

`--redact names.txt` scrubs product or client names from every line of output and
every HTML page (`Name=Replacement`, one per line, matched case-insensitively where
an identifier starts). `.\demo.ps1 demo1-real` is exactly this, pointed at a real
codebase that sits next to this repo (folder name in `..\code\real-estate.txt`),
and it refuses to run without the list. Nothing from that codebase is in this repo.

`legacy seams` and `legacy ratchet --estate` work the same way. The ratchet rules
are in `src/LegacyCtl/Estate/Rules.cs`; add the patterns your codebase has.

`triage` needs `signals.json`, which you can only fill in by talking to people.
That is the point.
