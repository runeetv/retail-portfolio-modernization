# Ground-Truth Benchmark — DO NOT EXPOSE TO THE ANALYZING AGENT

This folder contains the **answer key** for the reverse-engineering experiment.

When running an AI portfolio-discovery / modernization experiment against
LegacyRetailPortfolio, give the analyzing agent only the application source code
(`src/`), the database DDL (`database/`), and ordinary docs (`docs/`, `README.md`).
**Withhold this `benchmark/ground-truth/` folder.** Use it afterwards to score the
agent's findings.

## Files
| File | Contents |
|------|----------|
| `application-inventory.md` | Apps, responsibilities, project layout, bounded contexts |
| `application-architecture.md` | Per-app functionality + overlapping features (block diagrams) + tables used by each application |
| `business-capabilities.yaml` | Capability catalog with classification + evidence |
| `domain-entities.yaml` | C# model → SQL table mapping; multi-model tables |
| `capability-overlaps.yaml` | The six overlap scenarios |
| `business-rules.yaml` | Rules + implementation location (SQL vs C#) |
| `cross-app-workflows.yaml` | Workflows A–E with steps + test references |
| `dependencies.yaml` | Code + database + stored-proc dependencies |
| `evidence-map.yaml` | Intentional imperfections with source evidence |
| `modernization-opportunities.md` | Candidate rationalization findings |

All evidence references point to real files/methods in the repository.
