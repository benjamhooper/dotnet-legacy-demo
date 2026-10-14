# estate/ — Tallgrass Utilities billing, 2011 to present

A **fictional** utility-billing system, built to look like the decade-old .NET
estates this talk is about. Every name, number, rule, and commit count here is
invented. The *patterns* are real: each one has turned up, in some form, on an
enterprise modernization engagement. None of this is client code.

| Project | Built by the demo? | What it is there to show |
|---|---|---|
| `Tallgrass.Common` | yes | the gravity-well hub: `Db`, `AppSettings`, `Utils`, `Constants` |
| `Tallgrass.Data` | yes | static repositories over stored procedures |
| `Tallgrass.Business` | yes | a "temporary" project nine others depend on |
| `Tallgrass.Billing` | yes | `BillingManager`, the god class that demo 2 cuts a seam into |
| `Tallgrass.Reports` | specimen | an 800-line class that should be left alone (demo 4) |
| `Tallgrass.Integrations.Meter` | specimen | what good looks like: small, injected, changes weekly |
| `Tallgrass.Admin.Fixer` | specimen | a WinForms tool for hand-repairing production data; still .NET Framework 4.8, old-style `.csproj` |
| `Tallgrass.CustomerPortal.Classic` | specimen | WebForms (`.aspx` + code-behind), replaced in 2024, still deployed, still .NET Framework 4.8 |
| `lib/` | specimen | vendor DLLs checked into source control, referenced by `HintPath` (zero-byte placeholders) |
| `Tallgrass.Web`, `.Payments`, `.Batch`, `.Rates`, `.Notifications`, `.Tests` | specimen | coupling, `HttpContext` shims, `.Result`, `#if` forks |
| `Database/` | specimen | nine procedures; four make business decisions |

**Specimen** projects are read as text by `legacy map`, `legacy seams`,
`legacy triage` and `legacy ratchet`. They are deliberately not in
`DotnetLegacyDemo.sln` and several would not compile on their own (WinForms,
ASP.NET, NUnit). That is fine: nothing here is meant to run except the four
compiled projects.

`.history/` holds what `git log` and a week of conversations would tell you.
See `.history/README.md`.

## Pointing the tools at your own code

In a clone of this repository, replace `estate/` with a copy of your solution
folder, drop in your own `churn-12mo.tsv` from `git log`, and
leave `signals.json` empty until you have had the conversations.
