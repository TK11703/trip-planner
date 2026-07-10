// Built-in trip map interop. Renders trip event locations with Leaflet + OpenStreetMap tiles,
// fits all markers on first launch, then leaves native pan/zoom enabled. Leaflet is loaded on
// demand from the bundled assets under wwwroot/lib/leaflet so no map key reaches the browser.

// Single, swappable tile source (OpenStreetMap requires visible attribution).
const TILE_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const TILE_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

const maps = new Map();
let nextId = 1;
let leafletPromise = null;

function ensureLeaflet() {
    if (window.L) {
        return Promise.resolve();
    }
    if (leafletPromise) {
        return leafletPromise;
    }
    leafletPromise = new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-leaflet]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'lib/leaflet/leaflet.css';
            link.setAttribute('data-leaflet', '');
            document.head.appendChild(link);
        }
        const script = document.createElement('script');
        script.src = 'lib/leaflet/leaflet.js';
        script.async = true;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Failed to load Leaflet.'));
        document.head.appendChild(script);
    });
    return leafletPromise;
}

function configureDefaultIcon(L) {
    // Point Leaflet's default marker at the bundled images.
    delete L.Icon.Default.prototype._getIconUrl;
    L.Icon.Default.mergeOptions({
        iconUrl: 'lib/leaflet/images/marker-icon.png',
        iconRetinaUrl: 'lib/leaflet/images/marker-icon-2x.png',
        shadowUrl: 'lib/leaflet/images/marker-shadow.png'
    });
}

function buildPopup(point, dotNetRef) {
    const container = document.createElement('div');
    container.className = 'tp-map-popup';

    const heading = document.createElement('div');
    heading.className = 'fw-semibold';
    heading.textContent = point.title;
    container.appendChild(heading);

    if (point.location) {
        const sub = document.createElement('div');
        sub.className = 'small text-secondary';
        sub.textContent = point.location;
        container.appendChild(sub);
    }

    if (dotNetRef) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-sm btn-link p-0 mt-1';
        button.textContent = 'Open event';
        button.addEventListener('click', () => dotNetRef.invokeMethodAsync('OnMarkerActivated', point.trackedItemId));
        container.appendChild(button);
    }

    return container;
}

export async function init(element, points, dotNetRef) {
    await ensureLeaflet();
    const L = window.L;
    configureDefaultIcon(L);

    const map = L.map(element, { scrollWheelZoom: true });
    L.tileLayer(TILE_URL, { maxZoom: 19, attribution: TILE_ATTRIBUTION }).addTo(map);

    const markers = [];
    for (const point of points ?? []) {
        const marker = L.marker([point.latitude, point.longitude]).addTo(map);
        marker.bindPopup(buildPopup(point, dotNetRef));
        markers.push(marker);
    }

    if (markers.length === 1) {
        // A single point can't form a bounds; center on it at a reasonable zoom.
        map.setView(markers[0].getLatLng(), 13);
    } else if (markers.length > 1) {
        map.fitBounds(L.featureGroup(markers).getBounds(), { padding: [40, 40] });
    } else {
        // No markers: show a wide default view (the modal normally shows an empty state instead).
        map.setView([20, 0], 2);
    }

    // Blazor may render the modal before layout settles; nudge Leaflet to recompute size.
    setTimeout(() => map.invalidateSize(), 0);

    const id = 'tripmap-' + nextId++;
    maps.set(id, map);
    return id;
}

export function dispose(id) {
    const map = maps.get(id);
    if (map) {
        map.remove();
        maps.delete(id);
    }
}
