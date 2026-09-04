# Session Log — 2026-08-21

## Summary

Completed the **signed-contract upload** feature for the business registration workflow: replaced the old "paste a URL" flow with a real PDF/image file upload, wired it through the MSA media service (with a Local fallback), surfaced the resulting URL for browser verification, and fixed two blocking bugs (MudBlazor file binding and a missing MSA `FieldName` form field). Upload is currently **pending live verification against MSA** — user will run the test command below tomorrow.

## Context: contract workflow state (from prior sessions)

- Contract lifecycle: `None → Sent → (Confirmed | Signed) → Approved/Rejected`.
- **Confirmed** = business confirmed online via `/confirm-contract` (magic link + manual key + "I agree" checkbox).
- **Signed** = admin uploaded a signed PDF/image.
- Approval is unlocked by **either** Confirmed **or** Signed (Option 1).

## Changes made today

| File | Change |
|------|--------|
| `src/NonCash.Web/Components/Pages/Admin/RegistrationRequests.razor` | Fixed file binding: `MudFileUpload` now uses `@bind-Files` (was `@bind-Value`, which never updates in MudBlazor 9). Added `ResolveFileUrl` helper, a "View file" link on the Signed card, and the upload URL in the success snackbar. Injected `IConfiguration`. Sends `entity=business_registration_requests`, `uniqueCode={requestId}`, `fieldName=contract_file_url`. |
| `src/NonCash.Infrastructure/Services/MsaMediaClient.cs` | `UploadAsync` now takes a `fieldName` param and sends it as `FieldName`. MSA's `/api/Media/upload` rejects with `400 "The FieldName field is required."` without it. |
| `src/NonCash.Core/Interfaces/IDocumentStorageService.cs`, `MsaDocumentStorageService.cs`, `LocalStorageDocumentService.cs` | Threaded `fieldName` through `StoreAsync`. |
| `src/NonCash.API/Controllers/DocumentUploadController.cs` | Accepts and requires a `fieldName` form field. |
| `src/NonCash.Infrastructure/Services/MsaImageStorageService.cs` | Passes stable `FieldName="image"` (image `uniqueCode` already encodes the field). |

## Bug 1 — Upload button stayed disabled after selecting a file

- **Cause:** MudBlazor 9 `MudFileUpload` binds the selection via `Files`/`FilesChanged`, not `Value`. `@bind-Value` compiled but never set `_signedContractFile`, so the button's `Disabled=..._signedContractFile == null` stayed true.
- **Fix:** `@bind-Files="_signedContractFile"`.

## Bug 2 — MSA rejected upload: `The FieldName field is required.`

- **Cause:** The snackbar showed `API Error: BadRequest - {...FieldName...}` — the prefix is added by `MsaMediaClient` when MSA returns non-success, so the rejection came from MSA. MSA requires `FieldName` (plus `Entity` and `UniqueCode`) to build its storage folder as an extension of the client table.
- **Fix (final, per user):** MSA folder model = `Entity` (table name) / `UniqueCode` (record id) / `FieldName` (DB column storing the URL). For signed contracts send `Entity=business_registration_requests`, `UniqueCode={requestId}`, `FieldName=contract_file_url`. The file bytes still travel in the multipart part named `File`. (`FieldName` is the column, NOT the multipart part name.)

## How the upload works

- `MediaServiceConfig:ImageStorage` = `"MSA"` → `DocumentUploadController` uses `MsaDocumentStorageService` → `MsaMediaClient.UploadAsync` → MSA `/api/Media/upload`. Only the returned `RelativeUrl` is stored in `ContractFileUrl`; full URL composed at display time (`{CDN}/{RelativeUrl}`).
- If `ImageStorage` = `"Local"` → writes to API `wwwroot/uploads/signed_contracts/` and returns `/uploads/...`.
- Accepted: `.pdf, .jpg, .jpeg, .png`, max 10 MB.

## Test command for tomorrow (test MSA upload service directly)

Run from the repo root in PowerShell. Uses `curl.exe` (the real curl, not the PowerShell alias). Replace the `File=@...` path with a small real pdf/jpg/png on disk.

MSA folder model: `Entity` = client table name, `UniqueCode` = record id, `FieldName` = DB column that stores the URL.

```
curl.exe -s -X POST "http://45.119.87.247:8001/api/Media/upload" -H "X-Api-Key: 398CC787B0D44F638C74BBEAB5110188" -F "AppCode=NONCASH" -F "MediaType=documents" -F "Entity=business_registration_requests" -F "UniqueCode=uploadtest-001" -F "FieldName=contract_file_url" -F "File=@\"d:\GIT PROJECT\NonCash\test-upload.pdf\";type=application/pdf"
```

Expected on success: JSON with `isSuccess:true` and a `relativeUrl`. If MSA still returns a validation error, check its Swagger at `http://45.119.87.247:8001/openapi` for the exact expected `FieldName` value.

> Note: the `X-Api-Key` above is read from `src/NonCash.API/appsettings.json` (`MediaServiceConfig:ApiKey`). Treat it as a secret.

### Real end-to-end upload test (through our API, same path as the Blazor dialog)

Requires the API running at `https://localhost:7107`. Logs in as admin, then posts to `POST /api/v1/upload/document` (multipart fields: `file`, `entity`, `uniqueCode`). Uses an existing PNG so it runs immediately.

```powershell
$api = "https://localhost:7107"
$img = "d:\GIT PROJECT\NonCash\src\NonCash.API\wwwroot\uploads\voucher_plan_headers\1a5d971e-0997-43c0-a26f-e4c3aaed34a9_icon.png"

# 1) Login as admin -> get JWT
$loginJson = curl.exe -s -k -X POST "$api/api/v1/auth/login" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@123"}'
$token = ($loginJson | ConvertFrom-Json).token

# 2) Upload through the real endpoint (entity=table, uniqueCode=id, fieldName=column)
curl.exe -s -k -X POST "$api/api/v1/upload/document" -H "Authorization: Bearer $token" -F "file=@\"$img\";type=image/png" -F "entity=business_registration_requests" -F "uniqueCode=uploadtest-001" -F "fieldName=contract_file_url"
```

Success returns `{"success":true,"url":"<relativeUrl>"}`; that `url` is what is stored in `ContractFileUrl`.

## Build & test status

- `NonCash.Web` and `NonCash.Infrastructure` compile with 0 code errors (built to temp output dirs to bypass Visual Studio file locks).
- Full-solution rebuild requires stopping Visual Studio first (MSB3027/MSB3021 copy-lock errors otherwise).

## Next steps (tomorrow)

1. Run the MSA test command above to confirm the upload service accepts `FieldName=File`.
2. Rebuild (VS stopped) and run the app; upload a signed contract from the admin Registration Requests page.
3. Verify the success snackbar shows the URL and the "View file" link opens the document in the browser.
4. Then proceed to end-to-end approval (Confirmed or Signed → Approve).

---

# Update — 2026-08-22 (aligned to proven MediaStorageDemo contract)

User built a working demo at `D:\GIT PROJECT\MediaServiceAgency\MediaStorageDemo` (`UploadDemo.razor` + `Services\MsaMediaClient.cs`) and confirmed MSA uploads succeed (upload → get URL → open in browser). NonCash's `MsaMediaClient` was realigned to that proven contract.

## Proven MSA contract (from the demo)

- Multipart fields sent **as-is, no lowercasing**: `AppCode`, `MediaType`, `Entity`, `UniqueCode`, `FieldName`, plus file part `File`.
- `MediaType` is a MSA **enum in PascalCase plural**: demo uses `"Images"`. So documents use `"Documents"` (NOT lowercase `images`/`documents`).
- `Entity` / `FieldName` are logical names (demo: `Customer` / `ProfileImage`); MSA just builds folders from them.
- `DeleteAsync` (`/api/media/delete-by-metadata`) payload **includes `fieldName`**; empty `fieldName` acts as a wildcard (delete all fields for the record).
- Demo NonCash config: `AppCode=NONCASH`, `ApiKey=398CC787B0D44F638C74BBEAB5110188` (same as NonCash appsettings).

## NonCash changes made (2026-08-22)

| File | Change |
|------|--------|
| `MsaMediaClient.cs` | `UploadAsync` sends all values as-is (removed `.ToLowerInvariant()`). `DeleteAsync` now takes `fieldName` and includes it in the payload. |
| `MsaDocumentStorageService.cs` | `MediaType` const → `"Documents"`; delete-before-upload passes `fieldName`; public `DeleteAsync` passes `""` (wildcard). |
| `MsaImageStorageService.cs` | `MediaType` const → `"Images"`; delete-before-upload passes `"image"`; public `DeleteAsync` passes `""`. |

`NonCash.Infrastructure` compiles with 0 errors.

## Corrected raw MSA test command

```
curl.exe -s -X POST "http://45.119.87.247:8001/api/Media/upload" -H "X-Api-Key: 398CC787B0D44F638C74BBEAB5110188" -F "AppCode=NONCASH" -F "MediaType=Documents" -F "Entity=business_registration_requests" -F "UniqueCode=uploadtest-001" -F "FieldName=contract_file_url" -F "File=@\"d:\GIT PROJECT\NonCash\test-upload.pdf\";type=application/pdf"
```
