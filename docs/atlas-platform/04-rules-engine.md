# Module 4: Rules engine

Status: Draft 2, 2026-10-25. All open questions answered (section 9). Design only. No target code is changed.

## 1. Purpose

The rules decide what each user sees and can change: steps, sections, controls, buttons, dashboard tabs and row menus. Today the same rule logic is in six places, and each module service computes its own inputs (roles, state, owner). A new app must get correct rules from its JSON only. This module defines one rules engine for all targets, with one input context, a clear result when more than one rule matches, and the same result on load and on save.

Scope: `CPRule` (roles, states, owner), the rule modes, the default when no rule matches, the owner and derived roles, the start-up check and a rule preview. Related items: value-based `Effects` (module 1, 5.1), `OwnerFields` and `RuleMode` (module 1, 10.5), `Permissions` and the role flags (module 6, 4.3), dashboard rules (module 3, 4.4), the save pipeline (module 1, 5.9).

## 2. Current behaviour

### 2.1 The rule

```json
{ "Roles": ["MFO"], "States": ["DRAFT"], "IsOwner": true,
  "Visible": true, "Disabled": false,
  "EnableControls": [], "DisableControls": [], "ShowControls": [], "HideControls": [] }
```

- A condition that is not there matches all. `Roles` and `States` match if one value is the same (case is ignored). `IsOwner` must be equal. Evidence: `Data/Data/DTO/CPAtlasApplicationConfig.cs.txt:168-179, 308-345`.
- The rules of a target are examined in order. The **first** rule that matches is applied, and the loop stops (`break`). Evidence: `CPAtlasApplicationConfig.cs.txt:373, 447, 497`; `CPAtlasDashboardConfig.cs.txt:125, 174`.
- If no rule matches, the target keeps the values of the JSON (`Visible` true, `Disabled` false, unless the JSON sets them).

### 2.2 Where the rules run

| Target | Method | Evidence |
|---|---|---|
| Step (all its controls) | `CPApplicationStep.ApplyRules` | `CPAtlasApplicationConfig.cs.txt:308-380` |
| Section control and its children | `CPApplicationSection.ApplyRules` | `CPAtlasApplicationConfig.cs.txt:382-456` |
| Section button | `ApplyButtonRules` | `CPAtlasApplicationConfig.cs.txt:458-505` |
| Dashboard button, tab | `ApplyButtonRules`, `ApplyTabRules` | `CPAtlasDashboardConfig.cs.txt:86-184` |
| Dashboard row menu (roles only) | `ApplyMenuRules` | `CPAtlasDashboardConfig.cs.txt:60-84` |

- AIN, SQMSO and SQMSB call the section rules. TTD and CDT call no section or step rules. No service calls the step rules. Evidence: `CPAINService.cs.txt:62-63`; `CPSQMSOService.txt:142-152`; `CPSQMSBService.cs.txt:196-197`.

### 2.3 The inputs

Each service computes the roles, the state and `IsOwner` itself:

- AIN: `IsOwner` = "the user has no `Read` role". Evidence: `CPAINService.cs.txt:59`.
- SQMSO: `IsOwner` = `CreatedBy` is the display name of the user, or `SellerRMTeam` contains it. Evidence: `CPSQMSOService.txt:93-95`.
- SQMSB: in states with `CHECKSELLER`, an MFO user who owns the parent opportunity gets the role list `["Seller"]` in place of the real roles. Evidence: `CPSQMSBService.cs.txt:136-144`.

## 3. Findings

### RE-01 Six copies of the same rule logic (Medium for reuse)

The match of roles, states and owner is copied in six methods (2.2). The copies already differ: the menu rule does not reset its condition (module 3, DB-05), the step rule supports `ShowControls` / `HideControls` and the section rule does not, and the section rule applies `EnableControls` / `DisableControls` only to child controls. Each new module must also remember to call the rules (TTD does not). A correction must be made in six places.

### RE-02 The first matching rule wins, so a user with two roles can get less access (High)

The result depends on the order of the rules in the JSON, not on the roles of the user. Example (AIN, `AccountInfo`): a user with `Admin` and `Read` gets `IsOwner` = false (because the user has `Read`). The `Admin` rule needs `IsOwner` = true and does not match. The `Read` rule matches: the section is read-only for an admin. Evidence: `AIN.json.txt:14-30`; `CPAINService.cs.txt:59-62`.

Correction to module 1 (10.5): module 1 says "today the last matching rule wins". The code stops at the first match. Module 1 is corrected in the same change as this spec.

### RE-03 No matching rule gives full access (High)

If no rule matches, the target keeps the JSON values, which are usually visible and enabled. A gap in the rule list gives an editable form. Example (SQMSB, `OpportunityInformation`): an MFO user who owns a bid in the state `CLOSED TO BIDDING` matches none of the four rules, so the section stays enabled. Evidence: `SQMSB.json.txt:265-340`. Today the save does not check the rules (module 1, SM-14), so the change is saved.

### RE-04 `IsOwner` has a different meaning in each app (Medium)

AIN uses `IsOwner` to mean "can write". SQMSO and SQMSB use it for "my record", with a display name and a substring match (module 6, SE-04). SQMSB also makes a role (`Seller`) in code. A new app cannot reuse any of this from JSON.

### RE-05 The rules apply only to the display (High, security)

The rules change `Visible` and `Disabled` in the response. The save, the delete and the actions do not apply them again (module 1, SM-14; issue H4). This finding is in this module so that the engine is made for both uses; the correction is the save pipeline (module 1, 5.9) and the access check (module 6, 4.3).

### RE-06 Null references (Low)

A section rule on a control with no `ChildControls`, a step rule on a step with no `Controls`, and a menu with no `Rule` stop the request with a null reference. The current JSON has rules only on group controls with children, so it does not fail today. Evidence: `CPAtlasApplicationConfig.cs.txt:340, 421`; `CPAtlasDashboardConfig.cs.txt:77-80`.

### RE-07 Names in rules are not checked (Low)

A wrong control name in `EnableControls`, a wrong state in `States`, or a wrong role in `Roles` has no effect and gives no message. The rule then does not match, and RE-03 applies.

## 4. Reusable design

### 4.1 One engine

Add `IAtlasRuleEngine` with one method:

```csharp
RuleResult Evaluate(IReadOnlyList<CPRule>? rules, RuleContext context, RuleMode mode, RuleDefault fallback);
```

- `RuleContext`: the roles of the app (module 6, 4.4, with `RoleScope` of module 1), the role flags, the state, the owner relations (4.4), and the operation (`View` or `Edit`).
- `RuleResult`: `Matched`, `Visible`, `Disabled`, and the name lists `Show`, `Hide`, `Enable`, `Disable`.
- All targets use it: step, section control, child control, section button, dashboard tab, dashboard button and row menu. The six methods become thin callers. Each target applies the name lists in the same way (all four lists work for steps and for sections). This removes RE-01 and RE-06.
- The platform builds `RuleContext` one time for each request. A module service does not compute roles, state or owner. The platform calls the engine for all apps, also TTD.

### 4.2 Rule modes

`RuleMode` is set for each app (module 1, 10.5):

- `FirstMatch` (default): the current behaviour. The first matching rule is the result.
- `MostPermissive`: all matching rules count. `Visible` = true if one rule shows. `Disabled` = false if one rule enables. A control is in `Enable` if one rule enables it. This removes RE-02 for the apps that use it.

Decision 2026-10-25 (question 1): users can have more than one role in one app. TTD and the new apps use `MostPermissive`. AIN, SQMSO, SQMSB and CDT keep `FirstMatch` (no change in behaviour). For these apps, the order of the rules in the JSON stays important: put the rule with more access first. The case of RE-02 (AIN `Admin` + `Read`) stays until the AIN JSON changes (4.8, step 8 of section 7).

### 4.3 The default when no rule matches

New optional `RuleDefault` for each app: `Keep` | `ReadOnly` | `Hidden`.

- `Keep` (default): the current behaviour (RE-03).
- `ReadOnly`: a target with rules but no matching rule is visible and disabled.
- `Hidden`: a target with rules but no matching rule is not visible.
- A target with no rules is not changed by `RuleDefault`.
- The start-up check writes a warning for each target with rules in an app that uses `Keep`. This removes RE-03 for the apps that use `ReadOnly` or `Hidden`.
- Decision 2026-10-25 (question 2): new apps use `ReadOnly`. It is the default in the new-app JSON template. The existing apps keep `Keep` (no change).

### 4.4 Owner and derived roles

- `IsOwner` keeps its meaning "my record". It comes from `OwnerFields` (module 1, 10.5), compared by LAN ID (module 6, 4.5). An app with no `OwnerFields` has `IsOwner` = false.
- "Can write" is not an owner condition. A rule uses the role flags: `"Roles": ["@Write"]` (module 6, 4.3). AIN can move from its `IsOwner` rule to `@Write` (4.8).
- New optional `Relations` for each app: a named owner condition on the parent record of a family (module 1, section 11). Example for SQMSB:

```json
"Relations": {
  "Seller": { "Record": "Parent", "Fields": ["CreatedBy", "SellerRMTeam"], "States": ["UNDER REVIEW BY SELLER"], "Roles": ["MFO"] }
}
```

- A rule uses a relation in place of a role: `"Roles": ["~Seller"]`. The engine adds `~Seller` to the context if the user has one of `Roles`, the record is in one of `States`, and the user is in one of `Fields` of the parent record. This replaces the `CHECKSELLER` code (SQMSB) with JSON. This removes RE-04.
- `Record` is `Self` (the current record) or `Parent` (the parent record of a family).

### 4.4.1 Section owners

Requirement (2026-10-25, question 3): future apps have sections for users with special duties. Example: a form with 5 sections, and each section has one owner. A user with write access can change only the sections that the user owns. Some sections are hidden from a writer who is not the owner. The SQMS `Seller` is one case of this.

The design gives two ways. The JSON of each section selects one. Decision 2026-10-25: both are necessary; it depends on the app (and can differ for each section of one app):

- By role: the owner is a group of users. The app has one role for each section (for example `Compliance`, `Tax`). The section rule uses the role: `"Roles": ["Tax"]`. No new feature.
- By person in the record: the owner is a person named in a field of the record (for example `TaxReviewerLanId`). The app has one relation with `Record: Self` for each section, and the section rule uses it: `"Roles": ["~TaxReviewer"]`.

Example (by person), with `RuleDefault: ReadOnly`:

```json
"Relations": {
  "TaxReviewer": { "Record": "Self", "Fields": ["TaxReviewerLanId"] }
},
"Controls": [
  { "Name": "TaxSection", "Type": "section",
    "Rules": [
      { "Roles": ["~TaxReviewer"], "Disabled": false },
      { "Roles": ["@Admin"], "Disabled": false },
      { "Roles": ["@Write"], "Visible": false }
    ] }
]
```

The tax reviewer and the admin can change the section. Other writers do not see it. Readers see it read-only (`RuleDefault`). The same rules apply on save (4.6), so a writer cannot change a section of another owner with a changed request.

- Note: SQMSB today **replaces** the roles with `Seller`. A relation **adds** `~Seller`. The SQMSB JSON must keep the result the same when it moves (4.8).

### 4.5 Rules and `Effects`

- The rules run first, on the server. They give the base: which targets the user can see and change.
- The `Effects` of module 1 (5.1) run after the rules, in the client and again on save. An effect can hide, disable, require or set a value. It cannot show a target that a rule hides, or enable a target that a rule disables.
- Thus the rules decide access, and the effects only make the form easier to use.

### 4.6 The same rules on save

The save pipeline (module 1, 5.9) builds the same `RuleContext` from the **stored** record (state and owner before the change), and calls the engine. A change to a hidden or disabled field returns 400 with the field name. A save of a section with no matching rule and `RuleDefault` `ReadOnly` or `Hidden` returns 403. This, with module 6 (4.3), removes RE-05.

### 4.7 Start-up check

The start-up check (module 5, 6.8) also examines the rules of each app:

- Each name in `ShowControls`, `HideControls`, `EnableControls` and `DisableControls` is a control of the same target.
- Each role in `Roles` is a role of the app (or the parent app) in the active roles view, a flag (`@Read`, `@Write`, `@Admin`) or a relation (`~Name`). An unknown role is a warning, not an error (a role can be added later).
- Each state in `States` is a state of the app. The states come from the workflow definition (module 10) or an optional `States` list in the app JSON. An unknown state is an error.
- This removes RE-07.

### 4.8 Rule preview

New endpoint `GET v1/RulePreview?code=<Code>&role=<Role>&state=<State>&owner=<true|false>`, for users with `@Admin` in the app only. It returns the effective section, steps, buttons and dashboard for that context, with no record. The team uses it to test a new app and to compare the JSON before and after a change (for example AIN to `@Write`, SQMSB to `~Seller`). A test can also call the engine directly with a matrix of contexts. Decision 2026-10-25 (question 4): keep the preview.

## 5. Example: rules for a new app

```json
"RuleMode": "MostPermissive",
"RuleDefault": "ReadOnly",
"OwnerFields": ["CreatedByLanId"],
"Section": {
  "Controls": [
    {
      "Name": "Main", "Type": "section",
      "Rules": [
        { "Roles": ["@Read"], "Disabled": true },
        { "Roles": ["@Write"], "IsOwner": true, "States": ["Draft"], "Disabled": false },
        { "Roles": ["@Admin"], "Disabled": false }
      ],
      "ChildControls": [ ]
    }
  ]
}
```

A writer edits own drafts. An admin edits all records. Any other case is read-only (`RuleDefault`), also when the JSON has a gap.

## 6. Backward compatibility

- `CPRule` keeps all its properties. `Relations`, `RuleMode`, `RuleDefault`, the flag names and the relation names are additive and optional.
- The defaults (`FirstMatch`, `Keep`) give the current result for the current JSON. The engine has a test that runs the current JSON of AIN, SQMSO and SQMSB with a matrix of roles, states and owner values, and compares the result with the current methods. All cells must be the same.
- The step and section methods keep their names and signatures; they call the engine.
- The start-up check gives errors only for defects that do not work today (a wrong state name in a rule). Unknown roles and `Keep` give warnings.
- The rules on save (4.6) start in the log-only mode of module 6 (section 5).

## 7. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Add the engine with `FirstMatch` and `Keep`. Change the six methods to call it. Run the comparison test (section 6). | Low |
| 2 | Build `RuleContext` in the platform (roles view, `RoleScope`, flags, state, `OwnerFields`). Call the rules for all apps. | Low |
| 3 | Add the start-up check (4.7) with warnings. | Low |
| 4 | Add `MostPermissive` and `RuleDefault`. Use them for TTD (pilot) and new apps. | Low |
| 5 | Add `v1/RulePreview` (4.8). | Low |
| 6 | Apply the rules on save in log-only mode, then enforce (4.6). | Medium |
| 7 | Add `Relations`. Move SQMSB `CHECKSELLER` to `~Seller`. Compare with the preview. | Medium |
| 8 | Move AIN from `IsOwner` to `@Write`. Compare with the preview. | Low |
| 9 | Correct the note in module 1 (10.5): the first matching rule wins. | None |

## 8. Acceptance

- One engine gives the same result as the current methods for each cell of the AIN, SQMSO and SQMSB matrix (section 6).
- With `MostPermissive` (TTD), a user with `Read` and `Prepare` can edit own records. AIN with `FirstMatch` gives the same result as today.
- With `RuleDefault: ReadOnly`, the SQMSB case of RE-03 is read-only.
- A save that changes a disabled field returns 400 in enforce mode, also with a changed request.
- A wrong state name in a rule stops the start-up, and the log names the app, the target and the state.
- TTD rules run with no code in `CPTTDService`.
- `v1/RulePreview` returns 403 for a user with no `@Admin` in the app.

## 9. Open questions

1. Answered (2026-10-25): yes, users can have more than one role in one app. Only TTD and the new apps change to `MostPermissive`. The existing apps keep `FirstMatch` (4.2). The read-only script [sql/04-multi-role-users.sql](sql/04-multi-role-users.sql) lists these users, to check the rule order of the existing apps.
2. Answered (2026-10-25): `ReadOnly` for new apps (4.3). The existing apps keep `Keep`.
3. Answered (2026-10-25): future apps have section owners: each section has one owner, a writer can change only own sections, and some sections are hidden from other writers (4.4.1). `Seller` is the SQMS case. Follow-up answered (2026-10-25): both, it depends on the app. The design supports both, also in one form.
4. Answered (2026-10-25): yes, keep the rule preview (4.8) for `@Admin` users, in addition to the automated tests.
