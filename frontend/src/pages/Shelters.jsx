import { useEffect, useState } from 'react';
import { Check, House, Users } from 'lucide-react';
import { get, patch, post, put, recommendShelters, shortId } from '../api';
import { SectionHeading, Panel, Field, Button, Progress, Pill, ErrorMessage, Success, Empty, Loading, SafeTable } from '../components/UI';
import MapView from '../components/MapView';

const emptyShelter = {
  name: '', area: '', capacity: 25, accessible: false, facilities: '',
  latitude: 6.9271, longitude: 79.8612, contactName: '', contactPhone: '',
  contactEmail: '', openingHours: '', medicalFacilities: false,
  sanitationFacilities: false, foodAvailable: false
};

export default function Shelters({ user }) {
  const [items, setItems] = useState([]);
  const [requests, setRequests] = useState([]);
  const [crises, setCrises] = useState([]);
  const [allocations, setAllocations] = useState([]);
  const [supplyRequests, setSupplyRequests] = useState([]);
  const [shelterId, setShelterId] = useState('');
  const [requestId, setRequestId] = useState('');
  const [count, setCount] = useState(1);
  const [recommendations, setRecommendations] = useState([]);
  const [recommendationsRequested, setRecommendationsRequested] = useState(false);
  const [recommendationLoading, setRecommendationLoading] = useState(false);
  const [form, setForm] = useState(emptyShelter);
  const [editingId, setEditingId] = useState('');
  const [administratorUserId, setAdministratorUserId] = useState('');
  const [supply, setSupply] = useState({ shelterId: '', crisisId: '', category: 'Food', description: '', quantity: 1 });
  const [centre, setCentre] = useState({ name: '', type: 'AssistanceCentre', area: '', facilities: '', accessible: false, contactName: '', contactPhone: '', contactEmail: '', openingHours: '', latitude: 6.9271, longitude: 79.8612 });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const canManage = ['ShelterAdministrator', 'EmergencyCoordinator', 'SystemAdministrator'].includes(user.role);
  const canLogistics = ['LogisticsManager', 'EmergencyCoordinator', 'SystemAdministrator'].includes(user.role);
  const canAssignAdministrator = ['EmergencyCoordinator', 'SystemAdministrator'].includes(user.role);
  const shelters = items.filter(x => x.type === 'Shelter');

  const load = async () => {
    const [facilities, caseList, crisisList] = await Promise.all([
      get('/shelters/facilities'), get('/requests'), get('/crises')
    ]);
    setItems(facilities);
    setRequests(caseList.filter(r => r.category === 'Shelter' && ['Verified', 'Assigned'].includes(r.status)));
    setCrises(crisisList);
    const supplyList = (canManage || canLogistics) ? await get('/shelter-supply-requests') : [];
    setSupplyRequests(supplyList);
    if (canManage) setAllocations(await get('/shelters/allocations'));
  };
  useEffect(() => { load().catch(e => setError(e.message)); }, []);

  const act = async (fn, message) => {
    setSuccess('');
    setError('');
    try {
      await fn();
      await load();
      setSuccess(message);
      setError('');
    } catch (e) {
      setSuccess('');
      setError(e.message);
    }
  };

  const saveShelter = () => act(async () => {
    const payload = { ...form, capacity: Number(form.capacity), latitude: Number(form.latitude), longitude: Number(form.longitude) };
    if (editingId) await put(`/shelters/${editingId}`, payload);
    else await post('/shelters', payload);
    setEditingId('');
    setForm(emptyShelter);
  }, editingId ? 'Shelter details updated.' : 'Shelter created.');

  const recommend = async () => {
    setSuccess('');
    setError('');
    setRecommendationLoading(true);
    setRecommendationsRequested(false);
    setRecommendations([]);
    try {
      const result = await recommendShelters(requestId);
      setRecommendations(result.recommendations);
      setRecommendationsRequested(true);
      setError('');
    } catch (e) {
      setSuccess('');
      setError(e.message);
    }
    finally { setRecommendationLoading(false); }
  };

  return <>
    <SectionHeading kicker="TEMPORARY HOUSING / SHELTERS" title="Shelter network" subtitle="Manage facilities, reservations, physical occupancy and supply requests" />
    <ErrorMessage error={error} />
    <Success message={success} />
    <div className="stats-grid compact">
      <div className="inventory-summary"><House size={22} /><strong>{shelters.length}</strong><span>Registered shelters</span></div>
      <div className="inventory-summary"><Users size={22} /><strong>{shelters.reduce((n, s) => n + (s.occupancy || 0), 0)}</strong><span>Checked in</span></div>
      <div className="inventory-summary"><House size={22} /><strong>{shelters.reduce((n, s) => n + (s.available || 0), 0)}</strong><span>Unreserved spaces</span></div>
    </div>
    <div className="dash-grid">
      <Panel title="Shelter and assistance-centre locations" subtitle="Only facility locations are shown on this directory map">
        <MapView points={items.map(x => ({ ...x, type: 'shelter' }))} height={370} />
      </Panel>
      <Panel title="Capacity overview" subtitle="Reservations are separate from physical occupancy">
        <div className="shelter-list">{shelters.length ? shelters.map(s => <div className="shelter-list-item" key={s.id}>
          <div className="shelter-line"><strong>{s.name}</strong><Pill>{s.status}</Pill></div>
          <p className="muted sm">{s.area} · {s.facilities || 'No facilities listed'} · {s.accessible ? 'Accessible' : 'Accessibility not confirmed'}</p>
          <Progress value={s.occupancy + s.reserved} max={s.capacity} />
          <div className="shelter-line muted sm"><span>{s.occupancy} checked in · {s.reserved} reserved</span><span>{s.available} available / {s.capacity} beds</span></div>
          {canManage && <Button variant="outline" onClick={() => {
            setEditingId(s.id);
            setForm({
              name: s.name, area: s.area, capacity: s.capacity, accessible: s.accessible, facilities: s.facilities,
              latitude: s.latitude, longitude: s.longitude, contactName: s.contactName, contactPhone: s.contactPhone,
              contactEmail: s.contactEmail, openingHours: s.openingHours, medicalFacilities: s.medicalFacilities,
              sanitationFacilities: s.sanitationFacilities, foodAvailable: s.foodAvailable
            });
          }}>Edit shelter</Button>}
        </div>) : <Empty />}</div>
      </Panel>
    </div>

    {(canManage || canLogistics) && <>
      {canManage && <Panel title={editingId ? 'Edit shelter' : 'Register a shelter'} subtitle="Keep contact, access, capacity and facility information current">
        <div className="inline-form">
          <Field label="Name"><input value={form.name} onChange={e => setForm(s => ({ ...s, name: e.target.value }))} /></Field>
          <Field label="Area"><input value={form.area} onChange={e => setForm(s => ({ ...s, area: e.target.value }))} /></Field>
          <Field label="Total beds"><input type="number" min="1" value={form.capacity} onChange={e => setForm(s => ({ ...s, capacity: e.target.value }))} /></Field>
          <Field label="Latitude"><input type="number" step="any" value={form.latitude} onChange={e => setForm(s => ({ ...s, latitude: e.target.value }))} /></Field>
          <Field label="Longitude"><input type="number" step="any" value={form.longitude} onChange={e => setForm(s => ({ ...s, longitude: e.target.value }))} /></Field>
          <Field label="Contact name"><input value={form.contactName} onChange={e => setForm(s => ({ ...s, contactName: e.target.value }))} /></Field>
          <Field label="Contact phone"><input value={form.contactPhone} onChange={e => setForm(s => ({ ...s, contactPhone: e.target.value }))} /></Field>
          <Field label="Contact email"><input type="email" value={form.contactEmail} onChange={e => setForm(s => ({ ...s, contactEmail: e.target.value }))} /></Field>
          <Field label="Opening hours"><input value={form.openingHours} onChange={e => setForm(s => ({ ...s, openingHours: e.target.value }))} placeholder="e.g. 24 hours" /></Field>
          <Field label="Facilities"><input value={form.facilities} onChange={e => setForm(s => ({ ...s, facilities: e.target.value }))} /></Field>
          <label className="check-row"><input type="checkbox" checked={form.accessible} onChange={e => setForm(s => ({ ...s, accessible: e.target.checked }))} /> Accessible</label>
          <label className="check-row"><input type="checkbox" checked={form.medicalFacilities} onChange={e => setForm(s => ({ ...s, medicalFacilities: e.target.checked }))} /> Medical facilities</label>
          <label className="check-row"><input type="checkbox" checked={form.sanitationFacilities} onChange={e => setForm(s => ({ ...s, sanitationFacilities: e.target.checked }))} /> Sanitation</label>
          <label className="check-row"><input type="checkbox" checked={form.foodAvailable} onChange={e => setForm(s => ({ ...s, foodAvailable: e.target.checked }))} /> Food available</label>
          <Button disabled={!form.name || !form.area} onClick={saveShelter}>{editingId ? 'Save changes' : 'Create shelter'}</Button>
          {editingId && <Button variant="outline" onClick={() => { setEditingId(''); setForm(emptyShelter); }}>Cancel edit</Button>}
        </div>
        {editingId && <div className="inline-form">
          <Field label="Operating status"><select value={shelters.find(s => s.id === editingId)?.status || 'Open'} onChange={e => act(() => patch(`/shelters/${editingId}/status`, { status: e.target.value }), 'Operating status updated.')}>
            <option>Open</option><option>Closed</option><option>Maintenance</option>
          </select></Field>
          {canAssignAdministrator && <>
            <Field label="Responsible shelter administrator user ID"><input value={administratorUserId} onChange={e => setAdministratorUserId(e.target.value)} placeholder="User ID or leave blank to clear" /></Field>
            <Button variant="outline" onClick={() => act(() => patch(`/shelters/${editingId}/administrator`, { userId: administratorUserId || null }), 'Shelter administrator assignment updated.')}>Assign administrator</Button>
          </>}
        </div>}
      </Panel>}

      {canManage && <Panel title="Approve a household placement" subtitle="Recommendations support—not replace—authorized administrator confirmation">
        <div className="inline-form">
          <Field label="Verified shelter request"><select value={requestId} onChange={e => {
            setRequestId(e.target.value);
            setRecommendations([]);
            setRecommendationsRequested(false);
          }}>
            <option value="">Choose a verified request</option>{requests.map(r => <option value={r.id} key={r.id}>{shortId(r.id)} · {r.peopleAffected} people</option>)}
          </select></Field>
          <Button disabled={!requestId || recommendationLoading} onClick={recommend}>
            {recommendationLoading ? 'Recommending...' : 'Recommend shelters'}
          </Button>
          <Field label="Shelter"><select value={shelterId} onChange={e => setShelterId(e.target.value)}>
            <option value="">Select shelter</option>{shelters.filter(s => s.status === 'Open').map(s => <option value={s.id} key={s.id}>{s.name} ({s.available} open)</option>)}
          </select></Field>
          <Field label="People"><input type="number" min="1" value={count} onChange={e => setCount(e.target.value)} /></Field>
          <Button disabled={!requestId || !shelterId} onClick={() => act(() => post('/shelters/allocate', { requestId, shelterId, peopleCount: Number(count) }), 'Shelter space reserved. Check-in is recorded separately.')}>
            <Check size={16} /> Reserve space
          </Button>
        </div>
        {recommendationLoading && <Loading />}
        {recommendationsRequested && recommendations.length === 0 && <Empty text="No suitable shelter found" />}
        {recommendations.length > 0 && <div className="shelter-list">{recommendations.map(r => <div className="shelter-list-item" key={r.shelterId}>
          <div className="shelter-line"><strong>{r.name} · {r.matchScore}% match</strong><span>{r.freeSpaces} spaces available · {r.distanceKm} km straight-line distance</span></div>
          <div className="muted sm">{r.occupied}/{r.totalBeds} occupied</div>
          <p className="muted sm">{r.reasons.join(' · ')}</p>
          <Button variant={shelterId === r.shelterId ? 'primary' : 'outline'} onClick={() => setShelterId(r.shelterId)}>
            {shelterId === r.shelterId ? 'Selected for reservation' : 'Select this shelter'}
          </Button>
        </div>)}</div>}
      </Panel>}

      {canManage && <Panel title="Reservation and stay tracking" subtitle="Check-in/out changes physical occupancy; cancelling releases only reserved capacity">
        <SafeTable headers={['ALLOCATION', 'SHELTER', 'PEOPLE', 'STATUS', 'ACTION']} empty="No shelter allocations yet.">
          {allocations.map(a => <tr key={a.id}><td>{shortId(a.id)}</td><td>{shelters.find(s => s.id === a.shelterId)?.name || shortId(a.shelterId)}</td><td>{a.peopleCount}</td><td><Pill>{a.status}</Pill></td><td>
            {a.status === 'Reserved' && <>
              <Button variant="outline" onClick={() => act(() => post(`/shelter-allocations/${a.id}/check-in`, {}), 'Household checked in.')}>Check in</Button>
              <Button variant="outline" onClick={() => act(() => post(`/shelter-allocations/${a.id}/cancel`, {}), 'Reservation cancelled.')}>Cancel</Button>
            </>}
            {a.status === 'CheckedIn' && <Button variant="outline" onClick={() => act(() => post(`/shelter-allocations/${a.id}/check-out`, {}), 'Household checked out.')}>Check out</Button>}
            {!['Reserved', 'CheckedIn'].includes(a.status) && '-'}
          </td></tr>)}
        </SafeTable>
      </Panel>}

      {canManage && <Panel title="Shelter resource request" subtitle="Approved requests are linked into the existing logistics request queue">
        <div className="inline-form">
          <Field label="Shelter"><select value={supply.shelterId} onChange={e => setSupply(s => ({ ...s, shelterId: e.target.value }))}>
            <option value="">Choose shelter</option>{shelters.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select></Field>
          <Field label="Crisis"><select value={supply.crisisId} onChange={e => setSupply(s => ({ ...s, crisisId: e.target.value }))}>
            <option value="">Choose crisis</option>{crises.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select></Field>
          <Field label="Supply category"><select value={supply.category} onChange={e => setSupply(s => ({ ...s, category: e.target.value }))}>
            <option>Food</option><option>Water</option><option>Medicine</option><option>Other</option>
          </select></Field>
          <Field label="Quantity"><input type="number" min="1" value={supply.quantity} onChange={e => setSupply(s => ({ ...s, quantity: e.target.value }))} /></Field>
          <Field label="Details"><input value={supply.description} onChange={e => setSupply(s => ({ ...s, description: e.target.value }))} /></Field>
          <Button disabled={!supply.shelterId || !supply.crisisId || !supply.description} onClick={() => act(async () => {
            await post(`/shelters/${supply.shelterId}/supply-requests`, { ...supply, quantity: Number(supply.quantity) });
            setSupply(s => ({ ...s, description: '' }));
          }, 'Supply request submitted.')}>Request supplies</Button>
        </div>
        <SafeTable headers={['REQUEST', 'CATEGORY', 'QUANTITY', 'STATUS', 'ACTION']} empty="No supply requests.">
          {supplyRequests.map(s => <tr key={s.id}><td>{shortId(s.id)}</td><td>{s.category} · {s.description}</td><td>{s.quantity}</td><td><Pill>{s.status}</Pill></td><td>
            {s.status === 'Pending' && canManage && <>
              <Button variant="outline" onClick={() => act(() => patch(`/shelter-supply-requests/${s.id}/status`, { status: 'Approved' }), 'Request approved and sent to logistics.')}>Approve</Button>
              <Button variant="outline" onClick={() => act(() => patch(`/shelter-supply-requests/${s.id}/status`, { status: 'Rejected' }), 'Request rejected.')}>Reject</Button>
            </>}
            {canLogistics && s.status === 'Approved' && <Button variant="outline" onClick={() => act(() => patch(`/shelter-supply-requests/${s.id}/status`, { status: 'Dispatched' }), 'Supply request marked dispatched.')}>Dispatch</Button>}
            {canLogistics && s.status === 'Dispatched' && <Button variant="outline" onClick={() => act(() => patch(`/shelter-supply-requests/${s.id}/status`, { status: 'Delivered' }), 'Supply delivery recorded.')}>Delivered</Button>}
            {s.status === 'Approved' && s.assistanceRequestId && <span className="muted sm">Logistics case {shortId(s.assistanceRequestId)}</span>}
          </td></tr>)}
        </SafeTable>
      </Panel>}

      {canManage && <Panel title="Add an assistance centre" subtitle="Add non-shelter facilities to the shared directory">
        <div className="inline-form">
          <Field label="Name"><input value={centre.name} onChange={e => setCentre(s => ({ ...s, name: e.target.value }))} /></Field>
          <Field label="Type"><input value={centre.type} onChange={e => setCentre(s => ({ ...s, type: e.target.value }))} /></Field>
          <Field label="Area"><input value={centre.area} onChange={e => setCentre(s => ({ ...s, area: e.target.value }))} /></Field>
          <Field label="Facilities"><input value={centre.facilities} onChange={e => setCentre(s => ({ ...s, facilities: e.target.value }))} /></Field>
          <Field label="Latitude"><input type="number" step="any" value={centre.latitude} onChange={e => setCentre(s => ({ ...s, latitude: e.target.value }))} /></Field>
          <Field label="Longitude"><input type="number" step="any" value={centre.longitude} onChange={e => setCentre(s => ({ ...s, longitude: e.target.value }))} /></Field>
          <Button disabled={!centre.name || !centre.area} onClick={() => act(async () => {
            await post('/assistance-centres', { ...centre, latitude: Number(centre.latitude), longitude: Number(centre.longitude) });
            setCentre(s => ({ ...s, name: '', area: '', facilities: '' }));
          }, 'Assistance centre added.')}>Add centre</Button>
        </div>
      </Panel>}

      {canLogistics && !canManage && <Panel title="Shelter supply requests" subtitle="Approved shelter requests enter the existing logistics queue">
        <SafeTable headers={['REQUEST', 'CATEGORY', 'QUANTITY', 'STATUS', 'ACTION']} empty="No shelter supply requests.">
          {supplyRequests.map(s => <tr key={s.id}><td>{shortId(s.id)}</td><td>{s.category} · {s.description}</td><td>{s.quantity}</td><td><Pill>{s.status}</Pill></td><td>
            {s.status === 'Approved' && <Button variant="outline" onClick={() => act(() => patch(`/shelter-supply-requests/${s.id}/status`, { status: 'Dispatched' }), 'Supply request marked dispatched.')}>Dispatch</Button>}
            {s.status === 'Dispatched' && <Button variant="outline" onClick={() => act(() => patch(`/shelter-supply-requests/${s.id}/status`, { status: 'Delivered' }), 'Supply delivery recorded.')}>Delivered</Button>}
          </td></tr>)}
        </SafeTable>
      </Panel>}
    </>}
  </>;
}
