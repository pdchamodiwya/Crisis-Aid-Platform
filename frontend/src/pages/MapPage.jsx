import { useEffect, useState } from 'react';
import { MapPin } from 'lucide-react';
import { get } from '../api';
import { SectionHeading, Panel, ErrorMessage, Pill, Empty } from '../components/UI';
import MapView from '../components/MapView';
export default function MapPage() {
  const [shelters,setShelters]=useState([]),[requests,setRequests]=useState([]),[error,setError]=useState('');
  useEffect(()=>{Promise.all([get('/shelters'),get('/requests')]).then(([s,r])=>{setShelters(s);setRequests(r)}).catch(e=>setError(e.message));},[]);
  return <><SectionHeading kicker="GEOSPATIAL OVERVIEW" title="Operational map" subtitle="Locate shelters and reported needs; straight-line distance is not travel time"/><ErrorMessage error={error}/><Panel title="Service coverage map" subtitle="Teal markers: shelters · Orange markers: assistance requests"><MapView points={[...shelters.map(s=>({...s,type:'shelter'})),...requests.map(r=>({...r,name:`${r.category} request`,type:'request'}))]} height={480}/><p className="map-credit">Map tiles © OpenStreetMap contributors. Locations are supplied by authorised platform users. Do not use demo coordinates for actual emergency response.</p></Panel><div className="dash-grid"><Panel title="Shelter coverage" subtitle="Available response centres">{shelters.map(s=><div className="map-line" key={s.id}><MapPin size={16}/><span>{s.name} — {s.area}</span><Pill>{s.status}</Pill></div>)}{!shelters.length&&<Empty/>}</Panel><Panel title="Geographical request summary" subtitle="Latest location-linked reports">{requests.slice(0,10).map(r=><div className="map-line" key={r.id}><MapPin size={16}/><span>{r.category} · {Number(r.latitude).toFixed(3)}, {Number(r.longitude).toFixed(3)}</span><Pill>{r.urgency}</Pill></div>)}{!requests.length&&<Empty/>}</Panel></div></>;
}
