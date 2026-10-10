# Quality loop

Two scheduled agent jobs keep CShells simple and consistent. The roadmap and run log live in #209.

| File | Purpose |
|---|---|
| [PRINCIPLES.md](PRINCIPLES.md) | The standard the review job audits against. |
| [GLOSSARY.md](GLOSSARY.md) | The canonical domain vocabulary. |
| [DECISIONS.md](DECISIONS.md) | Declined proposals, so they are not raised again. |
| [metrics.sh](metrics.sh) | Size metrics posted with every review run. |

## Labels

| Label | Meaning |
|---|---|
| `audit:proposed` | Filed by an audit. Waiting for maintainer triage. |
| `audit:approved` | Approved. The fixer job may deliver it, including merging the PR once review and CI pass. |
| `audit:tracking` | The roadmap and run-log issue. |
| `area:*`, `priority:P1`–`P3`, `breaking-change` | Classification, set when the issue is filed. |

To decline a proposal, close it as *not planned*. Add a line to DECISIONS.md if the reasoning should outlive the issue.

## Jobs

**Weekly review.** Rotates through one focus area per week: public API, domain language, simplification, architecture, tests, docs, product. Each run:
- checks recent changes and the focus area against the principles and glossary;
- de-duplicates against open and closed issues and DECISIONS.md;
- files at most 5 new `audit:proposed` issues, and none while 10 or more are waiting for triage;
- posts a run summary with `metrics.sh` output on #209.

Every issue must say what it removes or merges. Additive proposals need a stated reason why removal is not the better answer.

**Fixer.** Each run delivers one `audit:approved` issue end to end: branch, implement, two-axis review, PR, CI and squash-merge. It picks the earliest approved issue in the order in #209 whose prerequisites are closed.
