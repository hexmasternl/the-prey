## 1. Dependencies & Configuration

- [x] 1.1 Install `leaflet`, `@asymmetrik/ngx-leaflet`, and their TypeScript types (`@types/leaflet`)
- [x] 1.2 Add `leaflet/dist/leaflet.css` to the `styles` array in `angular.json`

## 2. Playfield Service

- [x] 2.1 Create `src/ThePrey/src/app/playfields/playfield.model.ts` with the `Playfield` interface (id, name, isPublic, coordinates: `{lat, lon}[]`)
- [x] 2.2 Create `src/ThePrey/src/app/playfields/playfield.service.ts` with `getById(id: string)` (`GET /playfields/:id`) and `patchVisibility(id: string, isPublic: boolean)` (`PATCH /playfields/:id`)

## 3. Map Component

- [x] 3.1 Create standalone `PlayfieldMapComponent` at `src/ThePrey/src/app/playfields/playfield-map/playfield-map.component.ts`
- [x] 3.2 Accept `@Input() coordinates: {lat, lon}[]` and `@Input() fallbackCenter: {lat, lon} | null`
- [x] 3.3 On init, initialise a Leaflet map with OpenStreetMap tiles
- [x] 3.4 When `coordinates` has 3+ points, draw an `L.Polygon` with semi-transparent fill and call `fitBounds`
- [x] 3.5 When `coordinates` is empty and `fallbackCenter` is provided, centre the map on that position at zoom 15
- [x] 3.6 When `coordinates` is empty and `fallbackCenter` is null, centre the map at `[0, 0]` at zoom 2
- [x] 3.7 Mark the map container as non-interactive (`dragging.disable()`, `touchZoom.disable()`, etc.) — this is a preview only

## 4. Detail Page

- [x] 4.1 Create standalone Ionic page `PlayfieldDetailPage` at `src/ThePrey/src/app/playfields/detail/playfield-detail.page.ts`
- [x] 4.2 On `ionViewWillEnter`, load the playfield via `PlayfieldService.getById()` and store in a signal/property; show a loading spinner while in flight; show "not found" state on 404
- [x] 4.3 Render the playfield name
- [x] 4.4 Add an `ion-toggle` bound to `isPublic`; on change call `patchVisibility` and roll back on error with a toast notification
- [x] 4.5 Embed `PlayfieldMapComponent`; resolve the `fallbackCenter` by calling `Geolocation.getCurrentPosition()` when coordinates are empty (handle denial gracefully)
- [x] 4.6 Add a **Set Area** button that navigates to `/playfields/:id/area` (stub route acceptable for now)

## 5. Routing

- [x] 5.1 Add the `/playfields/:id` route to `app.routes.ts` pointing to `PlayfieldDetailPage` with `canActivate: [authGuardFn]`
- [x] 5.2 Add a stub `/playfields/:id/area` route (can point at a placeholder or the same detail page for now)

## 6. i18n

- [x] 6.1 Add translation keys for the detail page (name label, visibility toggle labels, Set Area button, not-found message, error toast) in all existing locale files under `src/ThePrey/src/assets/i18n/`
