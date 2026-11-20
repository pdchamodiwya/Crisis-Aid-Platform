import { useEffect, useState } from 'react';
import { Bell, CheckCircle2 } from 'lucide-react';
import { get, patch, formatDate } from '../api';
import { SectionHeading, Panel, Button, Empty, ErrorMessage } from '../components/UI';
export default function Notifications() {
  const [items,setItems]=useState([]),[error,setError]=useState('');
  const load=()=>get('/notifications').then(setItems).catch(e=>setError(e.message));
  useEffect(()=>{load()},[]);
  const mark=async id=>{try{await patch(`/notifications/${id}/read`,{});await load();}catch(e){setError(e.message)}};
  return <><SectionHeading kicker="MESSAGING CENTER" title="My notifications" subtitle="In-app messages remain available even if email delivery is unavailable"/><ErrorMessage error={error}/><Panel title="Message inbox" subtitle="Relevant updates from the response coordination team">{items.length?items.map(n=><div className={`notification-row ${!n.read?'unread':''}`} key={n.id}><span className="notification-glyph"><Bell size={18}/></span><div><strong>{n.title}</strong><p>{n.message}</p><small>{formatDate(n.at)}</small></div>{!n.read && <Button variant="outline" onClick={()=>mark(n.id)}><CheckCircle2 size={15}/> Mark read</Button>}</div>):<Empty text="No notifications yet. You will see case updates here."/>}</Panel></>;
}
