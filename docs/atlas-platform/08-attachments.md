# Module 8: Attachments

Status: Draft 3, 2026-11-18. All open questions answered (section 9). Decisions for the first platform app with attachments are in section 10; for a platform app they replace the parts of 4.4 and 4.5 that they name. Design only. No target code is changed.

## 1. Purpose

Some apps keep files with a record (for example trade instructions in CDT). A new app must get file upload, download and removal from its JSON, with the same access control as the record. This module defines one attachment service for all apps and removes the two High security issues of the current file code (H1, H2).

Scope: the `filepicker` control, the upload in `ManagePage`, the storage, `v1/Download`, `v1/RemoveAttachment`, the limits and the access checks. Related items: the access check (module 6, 4.3), the rules on save (module 4, 4.6), the registry (module 7), the audit (module 9).

## 2. Current behaviour

### 2.1 Upload

- The client sends each file as a form part `files` with the name `<ControlName>#$#<FileName>`. Evidence: `WebUi/.../cp-atlas-service.ts.txt:97-103`.
- `CPCDTService.SavePage` writes each file to a temporary folder `<BaseDirectory>/<CDTAttachmentFolderName>/<random number>`, reads it back with GZip compression into `CDT_Attachment.FileContent`, and deletes the folder. The part before `#$#` goes to `Type` (the control name). Evidence: `Service/Application/CPCDTServices.txt:276-289`; `Service/CPApplicationService.cs.txt:109-148`.
- The Section Master client sends an empty file list: `pageFileOutput` is never filled. The section services (SQMSO, SQMSB, TTD, AIN) do not read `files`. Evidence: `cp-atlas-sectionmaster.ts.txt:316, 337`; `CPAINService.cs.txt:91`; `CPTTDService.cs.txt:65`.

### 2.2 Load and download

- When a CDT step loads, `LoadAttachment` writes each attachment, decompressed, to `<BaseDirectory>/CDTAttachment/<AttachmentID>/<FileName>` if the folder is not there. It returns the URL `<HostedURL>\v1\Download?name=<FileName>&code=CDT&id=<AttachmentID>`. Evidence: `CPCDTServices.txt:403-424, 496-509`.
- `v1/Download` opens `<BaseDirectory>/<CDTAttachmentFolderName>/<id>/<name>` from disk and returns it. It does not use the database and it ignores `code`. Evidence: `Controllers/V1/CPAtlasController.cs.txt:72-90`.

### 2.3 Removal

- `v1/RemoveAttachment?code=&id=` sets `IsDeleted` on the `CDT_Attachment` row with that ID. Evidence: `CPAtlasController.cs.txt:92-97`; `CPCDTServices.txt:347-354`.

### 2.4 Storage

`dbo.CDT_Attachment`: `ID`, `CDT_GeneralID`, `Type`, `FileName`, `FileContent` (GZip), `CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`, `IsDeleted`. Evidence: `CP_Sharepoint.txt:222-238`.

## 3. Findings

### AT-01 A user can download any file from the server (High, security, issue H1)

`v1/Download` joins `id` and `name` to the folder path with no check. `id=..\..` and `name=appsettings.json` returns the configuration file (with the connection settings). Evidence: `CPAtlasController.cs.txt:78-79`.

### AT-02 A file name from the client can write outside the upload folder (High, security, issue H2)

`MapData` uses the text after `#$#` as a path part. A part named `General#$#..\..\x.aspx` writes the file two folders higher, and the clean-up does not remove it. Evidence: `CPApplicationService.cs.txt:115-121`.

### AT-03 Download and removal do not check access (High, security)

- Any signed-in user can download a file of any record (also with no CDT role), and can remove any attachment by ID. `RemoveAttachment` does not check that the attachment belongs to the app or to a record that the user can change (module 6, SE-01).
- An unknown ID gives HTTP 500 (`ToList()[0]`). Evidence: `CPCDTServices.txt:349`.

### AT-04 Files are copied to the web server disk and stay there (Medium)

- Each step load writes each attachment, decompressed, to the application folder. Nothing removes these copies.
- A removed attachment (`IsDeleted`) stays on disk, so its download URL continues to work.
- If the folder exists, the file is not written again. With more than one web server, a download from another server gives 404.
Evidence: `CPCDTServices.txt:496-509`; `CPAtlasController.cs.txt:78`.

### AT-05 Only CDT supports attachments (Medium for reuse)

The table, the folder setting, the download path and the service code are for CDT only. A Section Master app with a `filepicker` loses the selected files with no message (2.1). A new app needs its own table, service code and download path.

### AT-06 No limits on the files (Medium)

- No limit on the size, the type or the number of files. The default request limit of IIS (about 30 MB) is the only limit.
- The `Type` (control name) from the client is not checked against the controls of the form, so a file can be put on any control name.
- The temporary folder name is `new Random().Next()`. If two uploads get the same number, the second deletes the folder of the first (`Directory.Delete`).
Evidence: `CPApplicationService.cs.txt:111-113`; `CPCDTServices.txt:278-281`.

### AT-07 Display names and local time (Low)

`CreatedBy` and `ModifiedBy` are display names; the dates are `DateTime.Now`. See module 6 (4.5) and module 1 (5.9, step 9).

## 4. Reusable design

### 4.1 One attachment service

`IAtlasAttachmentService` (platform) does upload, list, download and remove for all apps. The app JSON only declares `filepicker` controls. `CPCDTService` keeps its `IAttachmentService` methods; they call the platform service.

### 4.2 Storage

- The file content stays in SQL Server, GZip-compressed, as today. No file is written to the web server disk.
- Decision 2026-10-25 (question 1): one attachment table for each app, `<Schema>.Attachment`, the same as the data and audit tables of module 5. The new-app SQL template makes it (part 3a of [sql/05-new-app-template.sql](sql/05-new-app-template.sql)), with a foreign key to `<Schema>.General` and an index on `RecordID`. The manifest names it: `"AttachmentTable": "PBSP.Attachment"`.
- Columns: `ID`, `RecordID`, `ControlName`, `FileName`, `ContentType`, `SizeBytes`, `FileContent`, `CreatedByLanId`, `CreatedDate` (UTC), `ModifiedByLanId`, `ModifiedDate` (UTC), `IsDeleted`, plus `CreatedBy` / `ModifiedBy` display names for the History window.
- CDT keeps `dbo.CDT_Attachment` (registered in its manifest as its attachment table). New nullable columns can be added later with a script; no existing column changes.

### 4.3 Upload

- The client keeps the form part format `<ControlName>#$#<FileName>`. The Section Master client fills `pageFileOutput` from its `filepicker` controls (the same code as the step page), so section apps send their files.
- The server accepts a file only if `ControlName` is a `filepicker` control of the effective form for this user (rules of module 4, 4.6: visible and enabled).
- The file name is `Path.GetFileName` of the part after `#$#`, with no path characters, at most 255 characters. The name is data only; it is never a path. This removes AT-02.
- The server reads the upload stream into GZip in memory. No temporary folder. This removes the `Random` collision of AT-06.
- Limits from the control JSON (defaults in the manifest): `Accept` (list of extensions), `MaxSizeMb`, `MaxFiles`. A file that breaks a limit returns 400 with the control name and the reason (module 1, 5.11). Platform maximum (decision 2026-10-25, question 2): 10 MB for each file. Default `Accept` list: `.pdf`, `.docx`, `.xlsx`, `.csv`, `.txt`, `.msg`, `.png`, `.jpg`. A control can set a lower size or a shorter list, not more. The server checks the extension and the first bytes of the file (the file signature) for the types that have one (PDF, Office, PNG, JPG). The IIS request limit must allow `MaxFiles` x 10 MB.
- The files are saved in the same transaction as the record. If the record save fails, no file is saved.

### 4.4 Download

- New endpoint `GET v1/Attachment/{appCode}/{id}`. The server finds the row by `id` in the attachment table of the app, checks `IsDeleted` = 0, checks the access `View` on the parent record (module 6, 4.3), and streams the decompressed content from the database. The response has `Content-Disposition: attachment` with the stored file name.
- `v1/Download?name=&code=&id=` stays for the current CDT URLs, but it uses the same steps: it finds the row by `id`, requires that `name` is the stored file name, checks the access, and streams from the database. It never reads a path from the parameters. This removes AT-01 and AT-04 with no change to the URLs.
- The load response gives the new URL for new apps and the current URL for CDT.

### 4.5 Removal

`v1/RemoveAttachment?code=&id=` keeps its parameters. The server checks that the attachment is in the table of that app, that it is not deleted, and that the user has `Edit` on the parent record and the `filepicker` control is enabled for the user. Then it sets `IsDeleted`. An unknown ID returns 404. This removes AT-03.

### 4.6 Audit

Each upload and each removal writes one audit entry for the record (module 9), with the control name and the file name (`Old` = removed file name). Decision 2026-10-25 (question 4): the functions are upload, download and remove only. A replaced file shows as one removal and one upload in the history. No file versions and no preview.

### 4.7 JSON

```json
{
  "Name": "SupportingDocuments",
  "Label": "Supporting documents",
  "Type": "filepicker",
  "Multiple": true,
  "Accept": [".pdf", ".xlsx", ".docx", ".msg"],
  "MaxSizeMb": 10,
  "MaxFiles": 5,
  "Validator": { "Required": false }
}
```

## 5. Backward compatibility

- The upload part format, `v1/ManagePage`, `v1/Download` and `v1/RemoveAttachment` keep their routes and parameters. `v1/Attachment/{appCode}/{id}` is new.
- The current CDT download URLs continue to work. Only bad requests (a path in `id` or `name`, a removed file, no access) now fail.
- `dbo.CDT_Attachment` and its data do not change. The GZip format does not change, so the current rows can be read.
- The access checks start in log-only mode (module 6, section 5).
- `Accept`, `MaxSizeMb` and `MaxFiles` are optional. With no value, the platform maximum (10 MB, default type list) applies. The current CDT controls have no `Accept`; a CDT file type that is not in the default list must be added to the CDT control JSON before the limits are turned on (step 7).

## 6. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Change `v1/Download` to read from the database by `id` (4.4). Remove the disk copy in `LoadAttachment`. | Low. Removes H1. |
| 2 | Change the upload to `Path.GetFileName` and memory GZip (4.3). | Low. Removes H2. |
| 3 | Add the access checks to download and removal, log-only first (4.4, 4.5). | Low |
| 4 | Add `IAtlasAttachmentService`; change CDT to call it. | Low |
| 5 | Use the attachment table of the new-app SQL template (part 3a). | Low |
| 6 | Fill `pageFileOutput` in the Section Master client (4.3). | Low |
| 7 | Add the limits (4.3) and the audit entries (4.6). | Low |
| 8 | Delete the old attachment copies in `<BaseDirectory>/CDTAttachment` on each server. | Low |

Steps 1 and 2 remove two High issues and can go first, in a small release of their own.

## 7. Acceptance

- `v1/Download?id=..\..&name=appsettings.json` returns 404 and reads no file.
- A part named `General#$#..\..\x.aspx` saves a file named `x.aspx` in the database and writes nothing to disk.
- A user with no role in the app gets 403 for the download in enforce mode.
- A removed attachment cannot be downloaded.
- A Section Master app with a `filepicker` saves, lists, downloads and removes files with no app code.
- A file larger than `MaxSizeMb` returns 400 with the control name. The record is not saved.
- A current CDT attachment URL downloads the same file as before.
- With two web servers, the download works from each server.

## 8. Security notes

- The download sends `X-Content-Type-Options: nosniff` and always `Content-Disposition: attachment`, so the browser does not run an uploaded HTML or script file.
- The file name in the response header is encoded (RFC 6266 `filename*`).
- Decision 2026-10-25 (question 3): no virus scan service. The endpoint protection on the servers, the type list and the file signature check (4.3) are the controls.

## 9. Open questions

1. Answered (2026-10-25): one attachment table for each app, `<Schema>.Attachment` (4.2). Added to the new-app SQL template, part 3a.
2. Answered (2026-10-25): 10 MB for each file; default types `.pdf`, `.docx`, `.xlsx`, `.csv`, `.txt`, `.msg`, `.png`, `.jpg` (4.3).
3. Answered (2026-10-25): no scan service. The endpoint protection on the servers and the type checks are enough (section 8).
4. Answered (2026-10-25): no. Upload, download and remove only; no file versions, no preview (4.6).

## 10. Decisions for the first platform app (2026-11-18)

The user made these decisions for P2, the first platform app with attachments. They apply to platform apps. CDT does not change: its `v1/Download` and `v1/RemoveAttachment` stay as they are. The CDT security fix (steps 1 to 3 of section 6) stays a separate item.

1. Download route: a new platform controller, `App/Platform/AtlasAttachmentController.cs`, with `GET v1/Attachment/{appCode}/{id}` (4.4). No target-owned file changes for attachments. Reason: the target `CPAtlasController` binds `code` as the `CPApplicationCode` enum in `Download` and `RemoveAttachment`, so a platform app code gets HTTP 400 before a platform line can run. `v1/Download` also reads the disk in the controller, so a platform line there would be in the CDT download path.
2. Removal on save, not at once. The user selects remove and accepts the question (the same dialog as CDT). The form then shows the file as "removed when you save". `v1/ManagePage` removes it in the same transaction as the record, the new files, the audit entries and the version check. A new optional field of the save request gives the IDs of the removed files (additive). If the user closes the form with no save, the platform removes nothing. A platform app does not use `v1/RemoveAttachment`, and the platform has no remove route.
3. Request size: the IIS default stays: about 28.6 MB for each request. The target sets no limit. The platform refuses a save whose new files are more than 25 MB in total. The client checks the total before it sends and shows a clear message ("Save at most 25 MB at one time"). The server checks again. The limit for each file stays 10 MB (4.3). If users often need more, a later additive change can raise the limit for the save route only.
