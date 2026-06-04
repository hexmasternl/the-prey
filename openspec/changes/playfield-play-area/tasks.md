## 1. Route and Page Scaffold

- [ ] 1.1 Create `src/ThePrey/src/app/playfields/area/` directory and generate the standalone `PlayfieldAreaPage` component
- [ ] 1.2 Add the `/playfields/:id/area` lazy-loaded route to `app.routes.ts`
- [ ] 1.3 Update the Set Area button in `PlayfieldDetailPage` to navigate to `/playfields/:id/area` (replacing any stub navigation)

## 2. Map Setup

- [ ] 2.1 Add `leaflet` and `@asymmetrik/ngx-leaflet` imports to `PlayfieldAreaPage` (both are already installed from the details page change)
- [ ] 2.2 Configure `[leafletOptions]` with `{ zoom: 15, center: [0,0], tap: false }` as defaults; set `[leafletLayers]` with the OSM tile layer
- [ ] 2.3 Bind `(leafletMapReady)` to a handler that stores the `L.Map` instance for use in click/geolocation logic

## 3. Initial State — Load Existing Coordinates

- [ ] 3.1 Inject `PlayfieldService` and call `getPlayfield(id)` on page load to fetch current coordinates
- [ ] 3.2 If coordinates exist: compute the centroid, fit the map bounds to the polygon, draw `L.CircleMarker` for each point, and draw the initial `L.Polygon`
- [ ] 3.3 If no coordinates exist: call `Geolocation.getCurrentPosition()` and centre the map; fall back to `[0, 0]` at world zoom if the call fails or is denied

## 4. Tap-to-Add Points

- [ ] 4.1 In the `leafletMapReady` handler, register `map.on('click', handler)` to capture the tapped `LatLng`
- [ ] 4.2 On each click, add an `L.CircleMarker` (radius 6, `color: '#22c55e'`, `fillColor: '#22c55e'`, `fillOpacity: 1`) to the map and push the coordinate to the internal points array

## 5. Live Polygon

- [ ] 5.1 After each point is added, check if the points array length is ≥ 3
- [ ] 5.2 If ≥ 3 points: remove the existing `L.Polygon` from the map (if any) and create a new one with `{ color: '#22c55e', fillColor: '#22c55e', fillOpacity: 0.25, weight: 2 }`
- [ ] 5.3 If < 3 points: ensure no polygon is on the map

## 6. Save and Cancel Actions

- [ ] 6.1 Add a **Save** button bound to `[disabled]="points.length < 3"` and a `(click)="onSave()"` handler
- [ ] 6.2 Add a `updateArea(id: string, coordinates: LatLng[])` method to `PlayfieldService` that issues `PATCH /playfields/:id` with the coordinate array
- [ ] 6.3 In `onSave()`: call `updateArea`, then call `Location.back()` on success; on HTTP error show an `IonToast` with the error message and keep the page open
- [ ] 6.4 Add a **Cancel** button that calls `Location.back()` immediately with no API call

## 7. UI Polish

- [ ] 7.1 Position Save and Cancel buttons as an `IonFooter` toolbar so they do not overlap the map
- [ ] 7.2 Show a loading indicator (e.g., `IonLoading`) while the initial playfield fetch and optional geolocation call are in progress
- [ ] 7.3 Ensure the map `div` fills the remaining viewport height after the header and footer are accounted for (CSS: `height: 100%` or `flex: 1`)
