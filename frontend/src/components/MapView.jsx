import { useEffect, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
export default function MapView({ points = [], center = [6.9271, 79.8612], height = 330, onLocation, zoom = 10 }) {
  const node = useRef(null), map = useRef(null), markers = useRef(null), callback = useRef(onLocation);
  useEffect(() => { callback.current = onLocation; }, [onLocation]);
  useEffect(() => {
    if (!node.current || map.current) return;
    map.current = L.map(node.current, { scrollWheelZoom: false }).setView(center, zoom);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap contributors' }).addTo(map.current);
    markers.current = L.layerGroup().addTo(map.current);
    map.current.on('click', e => callback.current?.({ latitude: +e.latlng.lat.toFixed(6), longitude: +e.latlng.lng.toFixed(6) }));
    return () => { map.current?.remove(); map.current = null; };
  }, []);
  useEffect(() => {
    if (!markers.current || !map.current) return;
    markers.current.clearLayers();
    const bounds = [];
    points.forEach(p => {
      if (!Number.isFinite(+p.latitude) || !Number.isFinite(+p.longitude)) return;
      const latLng = [+p.latitude, +p.longitude]; bounds.push(latLng);
      L.circleMarker(latLng, { radius: p.type === 'request' ? 8 : 10, fillColor: p.type === 'request' ? '#e88946' : '#188b9d', color: '#fff', weight: 2, fillOpacity: .92 })
        .bindPopup(`<strong>${String(p.name || 'Location').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('\"', '&quot;')}</strong>`).addTo(markers.current);
    });
    if (bounds.length > 1) map.current.fitBounds(bounds, { padding: [22, 22], maxZoom: 11 });
  }, [points]);
  return <div ref={node} className="map-shell" style={{height}} role="region" aria-label="Map showing requests and shelters" />;
}
