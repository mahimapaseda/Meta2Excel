# Meta Receipts Lite v1.3.1

## Fix Engagement Ad Set Branch Resolution

- **Branch Resolution for Engagement Ad Sets**: Corrected branch mapping for Meta Ads ad sets named with engagement prefixes (e.g. `Engagement ad set - AMB`, `engagement ad set 01 - Galle`, `Engagement ad set MAT`, `Engagement ad set NEG NEW`).
- **Accurate Branch Summary Totals**: Prevents ad sets with "engagement" in their naming from creating duplicate pseudobranches (`engagement`, `Engagement`) in the **Branch Summary** sheet, ensuring spend and impression figures roll up into their respective branch destinations (e.g. Galle, Ambalangoda, Matara, Negombo, etc.).
- **Added Regression Tests**: Extended `BranchResolverTests` with 22 test cases covering real-world engagement ad set naming conventions.

## Downloads

- `MetaReceiptsLite-Setup-v1.3.1.exe` — recommended installer
- `MetaReceiptsLite-v1.3.1.zip` — portable app with dependencies
- `SHA256SUMS-v1.3.1.txt` — release asset checksums
