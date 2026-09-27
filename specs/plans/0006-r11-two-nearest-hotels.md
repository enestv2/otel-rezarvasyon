# Delta Plan 0006-R11: Two nearest hotels on the detail map

- Spec: `specs/active/0006-oneri-pdf-belgesi.md`
- Parent plan: `specs/plans/0006-plan.md`
- Status: Verified

## Recommendation

- Keep Haversine straight-line distance from the event venue as the definition of "nearest"; break ties by original input order. This keeps the inset spatially local and does not alter route-based hotel recommendation ranking.
- Limit only the detail/inset map to two hotels. The overview continues to show all hotels and the selected hotel's full route.
- Update the visible detail heading to say two; zero, one, or two candidates continue to work as before.

## Scope and proof

- Change `DetailMarkerLimit` and the detail heading in `StaticMapComposition`.
- Update schematic composition tests to prove nearest-two selection, stable tie ordering, and unchanged all-hotel overview. Update PDF text assertions that currently mention six.
- Run `scripts/check`, obtain an independent read-only review, and map AC-24 to those tests.

**Approval:** Approved by the user on 2026-09-26. Implementation authorized within the scope above.

| Criterion | Proof |
|---|---|
| AC-24 | `SchematicStaticMapProviderTests.RenderAsync_composes_an_overview_and_a_detail_of_the_two_nearest_hotels` verifies shuffled-distance selection and that the request retains all seven hotels; `CreateDetail_keeps_original_order_when_nearest_distances_tie` verifies stable tie ordering. PDF text assertions verify the updated two-hotel legend. Focused schematic tests: 12 passed; `scripts/check` green (2 steps: build and full test suite). Independent read-only review found no remaining issues. |
