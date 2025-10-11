\# agents.md — Working Rules for Jules



\## 1) Guardrails (Respect the Stack, No Over-Documentation)

\- Use \*\*MySQL\*\* for persistence and \*\*Avalonia (MVVM) + C#\*\* for the application.

\- Keep outputs \*\*requirements-focused\*\*; avoid build instructions unless explicitly requested.

\- No changelog or error-log sections; keep docs short and canonical.



\## 2) Environment \& Preflight (Autonomous Setup)

\- Ensure a \*\*local MySQL\*\* instance and seed minimal reference data to run end-to-end tests.

\- Validate that all required inputs (listed in README §6) exist; if not, \*\*generate empty templates\*\* with canonical headers.

\- Run a \*\*preflight data check\*\*: field presence, type shape, and key coverage (ProjectKey, CustomerKey, PeriodKey).



\## 3) Data Contracts (Canonical \& Mappings)

\- Enforce the canonical dictionary in README §4.  

\- Apply the conversion rules in README §5.1 and the reconciliation rules in §5.2.  

\- Maintain \*\*alias maps\*\* for Customer and Project names; unresolved items must appear in \*\*Quarantine\*\*.



\## 4) Quality Gates (No Confirmation Required)

\- If errors or violations occur, \*\*fail fast\*\* with:  

&nbsp; - a human-readable summary;  

&nbsp; - an actionable list (which rows, what’s missing/mismatched).  

\- Do \*\*not\*\* ask for confirmation to proceed; surface what must be fixed.



\## 5) Functional Tests (Black-Box)

\- Verify first-run acceptance (README §9).  

\- Test scenarios:  

&nbsp; - Revenue without PAPD coverage → flagged.  

&nbsp; - PAPD approved with no activity → flagged.  

&nbsp; - Period mismatch → flagged.  

&nbsp; - Variance over threshold → flagged.  

&nbsp; - Exports match on-screen totals.



\## 6) Reporting Requirements

\- Provide portfolio and project reports + exception reports exactly as in README §8.  

\- Ensure exports (CSV/Excel) reproduce filtered views accurately.



\## 7) Naming Stability

\- Use the \*\*business names\*\* from README §4 across UI and files (stabilizes mapping); internally align synonyms to those names.



\## 8) Autonomy \& Updates

\- Where inputs have missing mandatory columns, \*\*create a remediation task list\*\* and generate an empty, properly headed file for user completion.

\- Refresh data end-to-end on demand and after file changes.





