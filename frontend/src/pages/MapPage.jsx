import { useEffect, useState } from 'react';
import { MapPin } from 'lucide-react';
import { get } from '../api';
import { SectionHeading, Panel, ErrorMessage, Pill, Empty, Button } from '../components/UI';
import MapView from '../components/MapView';

const coverageRoles = ['EmergencyCoordinator', 'CaseManager', 'LogisticsManager', 'ShelterAdministrator', 'SystemAdministrator'];
const preciseLocationRoles = ['EmergencyCoordinator', 'CaseManager', 'LogisticsManager', 'ShelterAdministrator', 'SystemAdministrator', 'Citizen'];

export default function MapPage({ user }) {
  const [centres, setCentres] = useState([]);
  const [requests, setRequests] = useState([]);
  const [areas, setAreas] = useState([]);
  const [coverage, setCoverage] = useState([]);
  const [layers, setLayers] = useState({ warehouses: [], shortages: [] });
  const [error, setError] = useState('');
  const [pickMode, setPickMode] = useState('');
  const [origin, setOrigin] = useState(null);
  const [destination, setDestination] = useState(null);
  const [route, setRoute] = useState(null);
  const [address, setAddress] = useState('');
  const canViewCoverage = coverageRoles.includes(user?.role);
  const canViewPreciseLocations = preciseLocationRoles.includes(user?.role);

  useEffect(() => {
    Promise.all([
      get('/map/service-centres'),
      get('/map/service-areas'),
      canViewPreciseLocations ? get('/requests') : Promise.resolve([]),
      canViewCoverage ? get('/map/coverage') : Promise.resolve([]),
      canViewCoverage ? get('/map/layers') : Promise.resolve({ warehouses: [], shortages: [] })
    ]).then(([centreList, areaList, requestList, coverageList, mapLayers]) => {
      setCentres(centreList);
      setAreas(areaList);
      setRequests(requestList);
      setCoverage(coverageList);
      setLayers(mapLayers);
    }).catch(e => setError(e.message));
  }, []);

  const chooseLocation = point => {
    if (!pickMode) return;
    if (pickMode === 'origin') setOrigin(point);
    if (pickMode === 'destination') setDestination(point);
    setPickMode('');
    setRoute(null);
    setAddress('');
  };

  const calculateRoute = async () => {
    if (!origin || !destination) return;
    try {
      const params = new URLSearchParams({
        fromLat: origin.latitude, fromLon: origin.longitude,
        toLat: destination.latitude, toLon: destination.longitude
      });
      setRoute(await get(`/map/route?${params.toString()}`));
      setError('');
    } catch (e) { setError(e.message); }
  };

  const reverseGeocode = async point => {
    if (!point) return;
    try {
      const params = new URLSearchParams({ latitude: point.latitude, longitude: point.longitude });
      const result = await get(`/map/reverse-geocode?${params.toString()}`);
      setAddress(result.display_name || 'No address returned by the provider.');
      setError('');
    } catch (e) { setError(e.message); }
  };

  return <>
    <SectionHeading kicker="GEOSPATIAL OVERVIEW" title="Operational map" subtitle="Road routes are provider-based; any straight-line fallback is labelled and is not travel time." />
    <ErrorMessage error={error} />
    <Panel title="Service coverage map" subtitle="Teal: facilities · Orange: requests/aggregated shortages · Purple: resource stores · Circles: operational areas">
      {pickMode && <p className="feedback" role="status" aria-live="polite">
        Pick mode active: {pickMode === 'origin' ? 'Route origin' : 'Route destination'}. Click the map to set it.
      </p>}
      <MapView
        points={[
          ...centres.map(s => ({ ...s, type: 'shelter' })),
          ...(canViewPreciseLocations ? requests.map(r => ({ ...r, name: `${r.category} request`, type: 'request' })) : []),
          ...layers.warehouses.map(x => ({ ...x, name: `${x.name} · ${x.availableQuantity} ${x.category}`, type: 'warehouse' })),
          ...layers.shortages.map(x => ({ ...x, name: `${x.category} shortage · ${x.shortageUnits} units`, type: 'request' })),
          ...(origin ? [{ ...origin, name: 'Route origin', type: 'route-origin' }] : []),
          ...(destination ? [{ ...destination, name: 'Route destination', type: 'route-destination' }] : [])
        ]}
        serviceAreas={areas}
        height={480}
        pickMode={pickMode}
        onLocation={chooseLocation}
      />
      <p className="map-credit">Map tiles © OpenStreetMap contributors. Do not use demo coordinates for actual emergency response.</p>
      <div className="inline-form">
        <Button variant={pickMode === 'origin' ? 'primary' : 'outline'} onClick={() => setPickMode('origin')}>Pick route origin</Button>
        <Button variant={pickMode === 'destination' ? 'primary' : 'outline'} onClick={() => setPickMode('destination')}>Pick route destination</Button>
        <Button disabled={!origin || !destination} onClick={calculateRoute}>Calculate route</Button>
        <Button variant="outline" disabled={!origin} onClick={() => reverseGeocode(origin)}>Address for origin</Button>
        <Button variant="outline" disabled={!destination} onClick={() => reverseGeocode(destination)}>Address for destination</Button>
      </div>
      {route && <div className="feedback">
        <strong>{route.distanceKm} km · {route.isFallback ? 'Straight-line fallback' : route.provider}</strong>
        {!route.isFallback && route.durationMinutes != null && <span> · {route.durationMinutes} minutes</span>}
        {route.isFallback
          ? <span> · {route.message || 'Not a road route or travel-time estimate.'}</span>
          : route.message && <span> · {route.message}</span>}
      </div>}
      {address && <p className="muted">{address}</p>}
    </Panel>

    <div className="dash-grid">
      <Panel title="Service centres" subtitle="Available shelter and assistance-centre locations">
        {centres.map(s => <div className="map-line" key={`${s.type}-${s.id}`}><MapPin size={16} /><span>{s.name} — {s.area}</span><Pill>{s.status}</Pill></div>)}
        {!centres.length && <Empty />}
      </Panel>
      <Panel title="Operational areas" subtitle="Configured service-area boundaries">
        {areas.map(a => <div className="map-line" key={a.id}><MapPin size={16} /><span>{a.name} · {a.radiusKm} km radius</span></div>)}
        {!areas.length && <Empty />}
      </Panel>
    </div>

    {canViewCoverage && <Panel title="Aggregated service coverage" subtitle="Area-level counts only; no citizen coordinates are returned">
      {coverage.map(area => <div className="map-line" key={area.id}>
        <strong>{area.name}</strong>
        <span>{area.openShelters} open shelters · {area.suitableAccessibleShelters} accessible with space · {area.openAssistanceCentres} assistance centres</span>
        <Pill>{area.coverage}</Pill>
      </div>)}
      {!coverage.length && <Empty />}
    </Panel>}

    {canViewCoverage && <div className="dash-grid">
      <Panel title="Aggregated shortages" subtitle="Verified request demand compared with nearby available stock">
        {layers.shortages.map((x, i) => <div className="map-line" key={`${x.id}-${x.category}-${i}`}>
          <strong>{x.name} · {x.category}</strong>
          <span>{x.shortageUnits} units short · {x.verifiedCases} verified cases</span>
        </div>)}
        {!layers.shortages.length && <Empty />}
      </Panel>
      <Panel title="Resource locations" subtitle="Available resources shown to authorized operational staff">
        {layers.warehouses.map(x => <div className="map-line" key={x.id}>
          <strong>{x.name}</strong><span>{x.area} · {x.availableQuantity} {x.category}</span>
        </div>)}
        {!layers.warehouses.length && <Empty />}
      </Panel>
    </div>}

    {canViewPreciseLocations && <Panel title="Geographical request summary" subtitle="Only your own location or locations visible to authorized operational staff">
      {requests.slice(0, 10).map(r => <div className="map-line" key={r.id}>
        <MapPin size={16} /><span>{r.category} · {Number(r.latitude).toFixed(3)}, {Number(r.longitude).toFixed(3)}</span><Pill>{r.urgency}</Pill>
      </div>)}
      {!requests.length && <Empty />}
    </Panel>}
  </>;
}
