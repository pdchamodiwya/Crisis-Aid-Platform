import { useEffect, useLayoutEffect, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
export default function MapView({ points = [], serviceAreas = [], center = [6.9271, 79.8612], height = 330, onLocation, zoom = 10, pickMode = '' }) {
  const node = useRef(null), map = useRef(null), markers = useRef(null), areaLayers = useRef(null), callback = useRef(onLocation);
  useLayoutEffect(() => { callback.current = onLocation; }, [onLocation]);
  useEffect(() => {
    if (!node.current || map.current) return;
    map.current = L.map(node.current, { scrollWheelZoom: false }).setView(center, zoom);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap contributors' }).addTo(map.current);
    markers.current = L.layerGroup().addTo(map.current);
    areaLayers.current = L.layerGroup().addTo(map.current);
    map.current.on('click', e => callback.current?.({ latitude: +e.latlng.lat.toFixed(6), longitude: +e.latlng.lng.toFixed(6) }));
    return () => {
      if (!map.current) return;
      map.current.stop();
      map.current.remove();
      map.current = null;
      markers.current = null;
      areaLayers.current = null;
    };
  }, []);
  useEffect(() => {
    if (!markers.current || !map.current) return;
    markers.current.clearLayers();
    const bounds = [];
    points.forEach(p => {
      if (!Number.isFinite(+p.latitude) || !Number.isFinite(+p.longitude)) return;
      const latLng = [+p.latitude, +p.longitude]; bounds.push(latLng);
      const isRoutePoint = p.type === 'route-origin' || p.type === 'route-destination';
      const color = p.type === 'route-origin' ? '#16803c' : p.type === 'route-destination' ? '#c62828' :
        p.type === 'request' ? '#e88946' : p.type === 'warehouse' ? '#7256a8' : '#188b9d';
      L.circleMarker(latLng, {
        radius: isRoutePoint ? 12 : p.type === 'request' ? 8 : 10,
        fillColor: color, color: '#fff', weight: isRoutePoint ? 3 : 2, fillOpacity: .92,
        interactive: !pickMode
      })
        .bindPopup(`<strong>${String(p.name || 'Location').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('\"', '&quot;')}</strong>`).addTo(markers.current);
    });
    if (bounds.length > 1) map.current.fitBounds(bounds, { padding: [22, 22], maxZoom: 11 });
  }, [points, pickMode]);
  useEffect(() => {
    if (!areaLayers.current) return;
    areaLayers.current.clearLayers();
    serviceAreas.forEach(area => {
      if (!Number.isFinite(+area.latitude) || !Number.isFinite(+area.longitude) ||
          !Number.isFinite(+area.radiusKm) || +area.radiusKm <= 0) return;
      const name = String(area.name || 'Operational area').replaceAll('&', '&amp;').replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;').replaceAll('\"', '&quot;');
      L.circle([+area.latitude, +area.longitude], {
        radius: +area.radiusKm * 1000, color: '#188b9d', fillColor: '#188b9d', fillOpacity: .06, weight: 2,
        interactive: !pickMode
      }).bindPopup(`<strong>${name}</strong><br>${area.radiusKm} km service radius`).addTo(areaLayers.current);
    });
  }, [serviceAreas, pickMode]);
  return <div ref={node} className="map-shell" style={{height}} role="region" aria-label="Map showing requests and shelters" />;
}
