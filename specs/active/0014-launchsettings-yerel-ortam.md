# Spec 0014 - Move local environment settings to launchSettings
- Status: In progress
- Mode: lite
- Plan: `specs/plans/0014-plan.md`

## Intent
Developers can run the API with local settings from launch profiles. Non-secret settings from `.env.example` and this machine's actual local secrets are in the local `launchSettings.json`. The real-valued file is ignored by Git; the repository contains a safe example profile. Production values are provided by Rancher Secret.

## Requirements
- Both `http` and `https` profiles include the required local settings.
- Both profiles retain `ASPNETCORE_ENVIRONMENT=Development`.
- The local `launchSettings.json` is Git-ignored and contains this machine's real values.
- The committed `launchSettings.example.json` contains no real secrets.
- Production environment variables are supplied through Rancher Secret.
- Environment variable names use ASP.NET Core mapping (`Section__Key`).
- `.env.example` remains a non-secret environment variable inventory for deployment/Kubernetes.

## Constraints and out of scope
- Real local secrets exist only in the ignored local launch settings file, not in committed files or this spec.
- This work does not create Kubernetes manifests, Helm charts, or Rancher configuration.
- Application configuration binding and behavior do not change.
- Provider removal in spec 0013 is out of scope.

## Acceptance criteria
- [x] AC-1 Both launch profiles contain all non-secret settings from `.env.example` with matching values.
- [x] AC-2 Both profiles use Development and application keys use `Section__Key`.
- [x] AC-3 Actual local API key, Geoapify key, and Mongo connection string exist only in the Git-ignored local file, not in committed files.
- [x] AC-4 Security documentation describes the ignored local file, safe example, and Rancher Secret production setup; `.env.example` has no real secrets.
- [x] AC-5 Both JSON files parse and profile settings match the environment inventory.

## Definition of Done
- [ ] Each acceptance criterion maps to repeatable evidence.
- [ ] `scripts/check` is green.
- [x] Independent review is complete and the finding is resolved.
- [x] Security/configuration documentation is updated.
- [ ] Spec is moved to `specs/done/`.

## Recommendation
Keep actual local values in the ignored `launchSettings.json`, provide a safe committed example, and supply production variables through Rancher Secret. This supports local development while keeping secrets out of source control.

## Implementation evidence
- AC-1/AC-5: A PowerShell JSON/environment inventory comparison checked both example and local `http`/`https` profile non-secret values against `.env.example`; all matched. Actual local Mongo connection setting was present and intentionally differs from the credential-free example URI.
- AC-2: Both profiles parsed as JSON and use `ASPNETCORE_ENVIRONMENT=Development`; application settings use double-underscore environment keys.
- AC-3: `git check-ignore` confirmed the actual local launch settings path is ignored. Secret values were not printed. The example profile has no API key values.
- AC-4: `docs/security.md` and `docs/git.md` document the ignored local file and Rancher Secret; security docs include User Secrets setup for both API and Geoapify keys.
- Independent review: completed read-only review; the API key setup documentation finding was addressed.
- Build: `dotnet build backend/RPAOtelRezervasyon.sln --nologo -warnaserror` succeeded with 0 warnings and 0 errors.
- Tests: `dotnet test backend/RPAOtelRezervasyon.sln --no-restore --nologo` reported Unit 97/97 passed, Architecture 5/5 passed, Provider 40/41 passed (one Windows Event Log permission failure), Integration 49 passed, 2 failed, 4 skipped. The two integration failures are `Post_pdf_uses_geoapify_base_map_and_includes_attribution` (expected 2 requests, got 1) and `Post_pdf_falls_back_to_schematic_map_when_geoapify_fails` (expected legacy text absent from PDF).
- `scripts/check` could not be invoked through this Windows shell: direct invocation yielded no script output, `sh` is unavailable, and `bash scripts/check` returned Access denied. Its configured build/test commands were run directly; the test suite is not green.

## Remaining Definition of Done
- [ ] `scripts/check` is green and all tests pass. Current failures are recorded above; spec remains active until resolved.
- [x] Independent review is complete and its finding is resolved.
- [x] Security/configuration documentation is updated.
- [ ] Move spec to `specs/done/` after all Definition of Done items pass.