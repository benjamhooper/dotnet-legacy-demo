# estate/.history

What `git log` would tell you about the estate, captured as fixtures so the demo
runs the same way on every machine, forever.

- `churn-12mo.tsv` is `git log --since=12.months --numstat` rolled up per file:
  commits that touched it, distinct authors. Regenerate against a real repo with:

  ```powershell
  git log --since="12 months ago" --format="@%an" --numstat |
    # ...see docs/hotspots.md for the twelve-line PowerShell that rolls this up
  ```

- `signals.json` is what you cannot get from source code at all: incidents,
  business criticality, what is on the roadmap, and usage. You get these by
  asking people. That conversation is the real work of week one.
