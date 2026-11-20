# Hand-off: ClientInflows (P2) gap report and design document

Date: 2026-11-19. From: requirements session (Claude). To: the next model, which writes the gap report and the
implementation design. Paste the prompt in section 6 to start.

## 1. State

- The requirements for P2 are recorded and closed: `docs/ClientInflows/ONBOARDING_ANSWERS.md`, decisions D1 to D34
  (commits 65a625c, 622714e). The screenshots in the same folder are the evidence (`form#1.jpeg`, `form2.jpeg`,
  `fields.jpeg`, `email1-3.jpeg`; `email4.jpeg` = copy of email3).
- The decisions table (D1 to D34) wins over the field and section tables when they differ. Later decisions replace earlier
  ones where they say so (D11 replaces the editable part of D10; D25 replaces "nobody deletes" in D22; D33 replaces the
  D9 LAN ID column).
- No design and no code exist for P2. No table script exists.

## 2. The app in short

- App `CFPINFLOW`, menu "Client Flows Portal - Inflow", top level. `CFPOUTFLOW` is a later, separate app (not in scope).
- One section form. Part 1 (Account Information, Flow Information, attachment type 1) and part 2 (Cash Receipt
  Confirmation, attachment type 2; shows after the first submit). The ICG group (`Write`) fills both parts.
- States: Draft (placeholder only), Submitted, Funds Received (end). T1 Submit part 1, T2 Submit part 2. No draft save,
  no cancel, no SLA. Part 1 read-only after T1; all read-only after T2.
- Emails: E1 (T1, ICG DL), E2 (T1, Investment Type DL(s) + RM), E3 (T2, Investment Type DL(s) + RM + CS Team DL;
  Cc RMCC + IMRG DL + Star FAS IFI Cash Team). "Fixed Income & Equity" = both FI and Equity lists. Old text kept.
- Account list from `AIN.AccountInventory` (not deleted), ngselect, "number - name"; fills 4 read-only fields.
  RM and RMCC emails: display name -> `CP_Atlas_User.DisplayName` -> `Email` (D33, D34). New AIN column RMCC (D32).
- Description and Comment: `richtext` (CKEditor 5), no pictures. Two `filepicker` fields, required, many files.
- Dashboard: tabs "Inflows" and "Inflow Pipeline"; row menu Edit, Delete (Write only), History. Date of destruction =
  created + 7 years, display only.
- Table: the target-made `CFP.Inflow` and `CFP.InflowAttachment` (D2); we may change them with additive scripts.

## 3. What to deliver

1. **Gap report** `docs/ClientInflows/GAP_REPORT.md`: each requirement (by D number or field number) in one group:
   (a) metadata only (JSON + SQL), (b) new generic platform code, (c) workflow engine or adapter change. Add the
   questions that block design. Show for each group what exists in the code today, with file names.
2. **Design document** `docs/atlas-platform/25-client-inflows-p2-design.md` (+ `.html` like the other modules) for the
   implementation: SQL scripts (release folder for P2), app manifest and form/dashboard JSON, the generic platform code
   (module 8 attachments, `richtext` control, reusable AIN account lookup, display-name recipient lookup,
   `--atlas-check` rule, engine adapter for section forms per module 10), the target integration steps for
   `INTEGRATION.md`, tests, and build order. Update `docs/atlas-platform/README.md` index.
3. Stop after the design. Ask the user to approve it before code.

## 4. Read first

- `AGENTS.md` (section "Atlas platform": rules, stop-and-ask, comment style, gates) and `PROJECT.md` section 0 and 22.
- `CODEX_HANDOFF.md` section A (state, findings F1 to F5, pending items).
- `docs/atlas-platform/README.md`, then modules 01, 04, 05, 06, 07 (section 5: new-app onboarding), 08 (section 10
  decisions: new controller `GET v1/Attachment/{appCode}/{id}`, removal on save, 25 MB per save), 09, 10, 11, 12
  (TTDV2 = the model for a new app), `NEW_APP_ONBOARDING_TEMPLATE.html`, `CODE_TRACE.md`.
- `atlas-platform/INTEGRATION.md`, `atlas-platform/sql/release-p1/` (040 = model app script),
  `docs/atlas-platform/sql/05-new-app-template.sql`, `06-grant-revoke-role-template.sql`.
- Target code (read-only, never edit, never copy keys or passwords):
  `target-implementation-read-only/lastet_target_files/latest_target_files_11182026/` (newest; `cp sharepoint.txt`
  has `CFP.Inflow`, `CFP.InflowAttachment`, `AIN.AccountInventory`, `dbo.CP_Atlas_User`). `CPSQMSOService` is the
  target model for a section form with a workflow.
- Engine notification recipients: `src/WorkflowEngine.Infrastructure/Notifications/NotificationRecipientResolver.cs`
  (the engine takes email addresses only; static address, workflow participant, request input).

## 5. Known facts and risks for the design

- Code status: module 8 is designed, not coded (no `filepicker` in the platform code). `richtext` is not coded.
  Module 10 (engine adapter) is not coded and is a stop-and-ask item.
- CKEditor 5: approved. Use the latest `ckeditor5` + `@ckeditor/ckeditor5-angular`, licenseKey `'GPL'`. The installed
  41.4.2 is in the CVE-2024-45613 range: replace it. Server-side HTML sanitizing; plain text on the dashboard and in
  History. Test with Angular 21. No picture adapter (no pictures in P2).
- `CFP.Inflow` gaps: no LanId columns, no RowVersion, `Description`/`Comment` are `varchar(max)` (not Unicode), no
  column for "Funds have been received", no state column. `CFP.InflowAttachment` uses `InflowId` + `Type`; module 8
  expects one `<Schema>.Attachment` table with `ControlName` as category: decide reuse or a new table, additive only.
- Audit: the target design uses `ReferenceID`/`ReferenceType` (B85); a CFP audit table must follow it.
- `AIN.AccountInventory`: target table owned by the AIN app. The user approved one new column RMCC (D32) here by a
  local script; the target team adds it there. The account lookup must be generic (4+ apps will use it).
- Display names are not unique (see the TTDV2 copy script). D34 gives the rules. `--atlas-check` must still exit 0 in
  the platform gate: decide how the AIN name check reports (for example a separate section that does not count as a
  gate warning, or a switch) and ask the user.
- Local email tests use smtp4dev; real DLs only at the target. Use placeholder addresses in local SQL.
- F2: target `v1/Download` binds `code` as an enum; module 8 avoids it with the new platform controller.
- Stop-and-ask: changes to persisted columns or stable identifiers, a new target file that must change, a new
  package (CKEditor is approved), engine adapter work, any `DateTime.UtcNow`. Never touch `050`/`090`.
- Working style: one question at a time, short answers, ASD-STE100 (about 80%) for documents, commit each step, push
  only when asked. Update `CODEX_HANDOFF.md` section A at the end.

## 6. Prompt for the next model

```text
Read AGENTS.md, then docs/ClientInflows/HANDOFF_GAP_REPORT_AND_DESIGN.md, then the files in its section 4.
The P2 app ClientInflows (CFPINFLOW) has closed requirements in docs/ClientInflows/ONBOARDING_ANSWERS.md (D1-D34).
Task 1: write docs/ClientInflows/GAP_REPORT.md (metadata only / new generic platform code / engine or adapter change,
with file references to the current code, and the questions that block design). Show it to me and ask the blocking
questions one at a time.
Task 2: after my answers, write the implementation design docs/atlas-platform/25-client-inflows-p2-design.md (+ .html),
as described in section 3 of the hand-off. Keep the platform generic: no CFPINFLOW-specific C# or TypeScript.
Do not write code or SQL scripts yet. Stop and ask on the AGENTS.md stop-and-ask items. Commit each step; do not push.
Update CODEX_HANDOFF.md section A at the end.
```
