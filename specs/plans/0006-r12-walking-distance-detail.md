# Delta Plan 0006-R12 — Walking metrics for nearby hotels

- Spec: `specs/done/0006-oneri-pdf-belgesi.md`
- Parent plan: `specs/plans/0006-plan.md`
- Supersedes detail-selection rule: `specs/plans/0006-r11-two-nearest-hotels.md`
- Status: Complete
- Approved by / on: User (conversation: “onaylıyorum”), 2026-09-26

## Recommendation

For PDF requests only, query Geoapify Routing with `mode=walk` for hotels whose existing measured road distance is strictly below 3,000 m. Keep the recommendation's drive/road metrics, ranking, and JSON response unchanged. Show walking distance and duration in separate PDF columns. Select up to two successfully routed eligible hotels by walking distance for the detail panel and draw their walking geometries; preserve all markers and the selected full route in the overview. If walking metrics cannot be retrieved, show “Alınamadı”. If metrics arrive without geometry, keep them in the table but omit that marker from the detail panel rather than drawing a guessed line. If no walking geometry succeeds, render overview only.

## Blast radius

- `backend/src/RPAOtelRezervasyon.Domain/Abstractions/IWalkingRouteDistanceProvider.cs` — walking-specific route capability without changing the JSON/ranking route contract.
- `backend/src/RPAOtelRezervasyon.Domain/Models/RecommendationReport.cs` — optional walking metrics on PDF report rows.
- `backend/src/RPAOtelRezervasyon.Domain/Models/StaticMapRequest.cs` — optional walking metrics/path per map marker; existing `Path` remains the overview route.
- `backend/src/RPAOtelRezervasyon.Application/Modules/Reporting/RecommendationReportService.cs` — query only `Road` candidates with distance `< 3000`, map outcomes to PDF rows/markers, tolerate individual route failures.
- `backend/src/RPAOtelRezervasyon.Infrastructure/Providers/Geoapify/GeoapifyRouteDistanceProvider.cs` — keep configured routing mode for existing calls and add explicit walking-mode call.
- `backend/src/RPAOtelRezervasyon.Infrastructure/Providers/Geoapify/GeoapifyServiceCollectionExtensions.cs` — register the walking interface to the Geoapify adapter with the existing rate limiter, retry, and timeout policies.
- `backend/src/RPAOtelRezervasyon.Infrastructure/Providers/StaticMap/StaticMapComposition.cs` — select detail markers by walking distance and use their walking paths; overview ranks stay unchanged.
- `backend/src/RPAOtelRezervasyon.Infrastructure/Providers/Reporting/QuestPdfRecommendationReportRenderer.cs` — separate walking distance/time columns and unavailable/not-queried labels.
- `backend/tests/RPAOtelRezervasyon.UnitTests/Reporting/RecommendationReportServiceTests.cs` — threshold, mode-provider outcomes, report values, and no-call behavior for JSON path.
- `backend/tests/RPAOtelRezervasyon.ProviderTests/GeoapifyProviderTests.cs` — `mode=walk`, metrics/geometry parsing, failure mapping, and drive default preservation.
- `backend/tests/RPAOtelRezervasyon.ProviderTests/SchematicStaticMapProviderTests.cs` — walk-distance order/ties/path, overview-all-markers, no-successful-walk fallback.
- `backend/tests/RPAOtelRezervasyon.IntegrationTests/RecommendationsPdfEndpointTests.cs` — additional PDF columns, “Alınamadı”, unchanged JSON/PDF recommendation values.
- `backend/tests/RPAOtelRezervasyon.IntegrationTests/Support/ApiTestFactory.cs` and focused fake walking provider — deterministic outcomes and call recording.
- `docs/domain.md` — clarify the supplemental walking metrics exception to BR-9 and the unchanged recommendation values.
- `docs/architecture.md` — document PDF-only walking routing calls and their scope.

## Ordered steps

1. Add the walking route interface and report/map model fields; add model invariants/tests.
2. Extend the Geoapify adapter with a per-call `walk` mode while retaining the configured default for all existing routes; register it under the walking capability and test requests/errors.
3. In report generation, filter eligible measured road candidates (`DistanceKind.Road` and `< 3000 m`), request walking routes without blocking the regular recommendation flow, and map unavailable outcomes without fabricating data.
4. Use successful walking distance/path only for detail selection/rendering; preserve overview marker set and selected overview route. Add schematic/provider tests.
5. Add walking values to the PDF table with explicit distinction between out-of-scope and unavailable values; verify JSON recommendation parity.
6. Update domain/architecture docs; run `scripts/check`, obtain independent read-only review, address real findings, and complete the AC proof map.

## Risks and recommendation

- **Provider call volume:** up to 20 extra routes per PDF request because the hotel list permits 20. Recommendation: keep the strict `< 3,000 m` road-distance filter, use existing Geoapify rate limiting/retry policy, and make no walking requests for the JSON endpoint. Official docs support `mode=walk` and return distance, time, and GeoJSON geometry: [Geoapify Routing API](https://apidocs.geoapify.com/docs/routing/). Geoapify pricing varies by route length and plan; verify actual account quota before deploying.
- **Partial provider failures:** recommendation: preserve PDF generation, label walking fields “Alınamadı”, and draw only successful walking routes in detail; if all eligible routes fail, keep overview only. This avoids invented measurements and respects the existing graceful map fallback.
- **Metric meaning:** walking values are supplemental and must never replace drive values in the selected hotel card, table's existing route columns, or JSON. Recommendation: label the new columns explicitly “Yürüyüş mesafesi” and “Yürüyüş süresi”.

## Criterion ↔ test map

| Acceptance criterion | Proven by |
|---|---|
| AC-25 | Unit report-service tests record walk requests and assert only measured `Road` candidates `< 3,000 m` are requested; renderer/PDF tests assert separate walking columns and unchanged drive values; JSON endpoint test asserts no walking calls and unchanged payload. |
| AC-26 | Composition/provider tests assert walking-distance ordering, recommendation-order tie break, and walking geometry in detail; assert overview marker ranks and selected drive route unchanged; failure tests assert “Alınamadı” and overview-only when all walking calls fail. |

**Plan approval:** Approved by the user on 2026-09-26.
## Plan amendment (R-07) — Approved

The implementation needs these additional or corrected paths; behavior scope is unchanged:

- Add `backend/src/RPAOtelRezervasyon.Api/Program.cs` — inject the walking provider into the PDF-only report service.
- Add `backend/tests/RPAOtelRezervasyon.ProviderTests/HostedStaticMapProviderTests.cs` — existing hosted-map tests must supply walking-route metrics to continue proving the detail-map request/fallback behavior.
- Correct the Geoapify test path from `GeoapifyRouteDistanceProviderTests.cs` to the existing `backend/tests/RPAOtelRezervasyon.ProviderTests/GeoapifyProviderTests.cs`.
- Add `backend/tests/RPAOtelRezervasyon.UnitTests/Fakes/FakeWalkingRouteDistanceProvider.cs` and `backend/tests/RPAOtelRezervasyon.IntegrationTests/Support/FakeWalkingRouteDistanceProvider.cs` — deterministic route outcomes and proof of threshold/PDF-only calls.

**Approval:** Approved by the user on 2026-09-26 (conversation: “evet”).

## Completion evidence

- `scripts/check`: green on 2026-09-26; build succeeded with 0 warnings/errors; unit 94 passed, provider 72 passed, architecture 5 passed, integration 51 passed / 4 skipped; 0 failures.
- Independent read-only review on 2026-09-26: no findings.
- Acceptance evidence: AC-25 and AC-26 proof map above and checked acceptance criteria in the shipped spec.
