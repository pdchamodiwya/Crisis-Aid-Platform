import React, { useEffect, useState } from 'react';
import { HeartHandshake, LayoutDashboard, ClipboardList, HandHeart, Boxes, House, UsersRound, HandCoins, MapPinned, ShieldCheck, Bell, LogOut, Menu, X, RadioTower, AlertTriangle, ChartNoAxesCombined, UserRoundCog, BookOpen, Megaphone, Building2, MessageSquareHeart, BrainCircuit, Home } from 'lucide-react';
import { getSession, setSession, can, isStaff, post } from './api';
import Auth from './pages/Auth';
import Dashboard from './pages/Dashboard';
import Requests from './pages/Requests';
import Citizen from './pages/Citizen';
import Resources from './pages/Resources';
import Shelters from './pages/Shelters';
import Volunteers from './pages/Volunteers';
import Donations from './pages/Donations';
import MapPage from './pages/MapPage';
import Admin from './pages/Admin';
import Notifications from './pages/Notifications';
import Crises from './pages/Crises';
import Analytics from './pages/Analytics';
import Account from './pages/Account';
import Guide from './pages/Guide';
import Alerts from './pages/Alerts';
import Household from './pages/Household';
import Feedback from './pages/Feedback';
import Partners from './pages/Partners';
import DecisionLab from './pages/DecisionLab';
const allNav = [
  {key:'dashboard',label:'Overview',icon:LayoutDashboard,staff:true},
  {key:'crises',label:'Crisis operations',icon:AlertTriangle},
  {key:'alerts',label:'Public alerts',icon:Megaphone},
  {key:'partners',label:'Partner organisations',icon:Building2,roles:['EmergencyCoordinator','PartnerOfficer','LogisticsManager','SystemAdministrator']},
  {key:'decision',label:'Decision intelligence',icon:BrainCircuit,roles:['EmergencyCoordinator','CaseManager','LogisticsManager','VolunteerCoordinator','SystemAdministrator']},
  {key:'analytics',label:'Impact & analytics',icon:ChartNoAxesCombined,staff:true},
  {key:'requests',label:'Case management',icon:ClipboardList,staff:true},
  {key:'citizen',label:'Request assistance',icon:HandHeart},
  {key:'household',label:'My household',icon:Home},
  {key:'feedback',label:'Assistance feedback',icon:MessageSquareHeart},
  {key:'resources',label:'Resources',icon:Boxes,staff:true},
  {key:'shelters',label:'Shelter network',icon:House},
  {key:'volunteers',label:'Volunteers',icon:UsersRound},
  {key:'donations',label:'Donations',icon:HandCoins},
  {key:'map',label:'Operational map',icon:MapPinned},
  {key:'notifications',label:'Notifications',icon:Bell},
  {key:'admin',label:'Governance & audit',icon:ShieldCheck,roles:['SystemAdministrator','EmergencyCoordinator']},
  {key:'account',label:'My profile',icon:UserRoundCog},
  {key:'guide',label:'System features',icon:BookOpen}
];
export default function App() {
  const [session,setAuth] = useState(getSession());
  const [active,setActive] = useState(isStaff(getSession()?.user)?'dashboard':'citizen');
  const [menu,setMenu]=useState(false);
  const [oauthBusy,setOauthBusy]=useState(false);
  const [oauthError,setOauthError]=useState('');
  useEffect(() => {
    const url = new URL(window.location.href);
    const ticket = url.searchParams.get('google_ticket');
    const error = url.searchParams.get('google_error');
    if (!ticket && !error) return;
    url.searchParams.delete('google_ticket'); url.searchParams.delete('google_error');
    window.history.replaceState({}, '', url.pathname + url.search + url.hash);
    if (error) { setOauthError(error); return; }
    setOauthBusy(true);
    post('/auth/google/exchange', { ticket }).then(auth => {
      setSession(auth); setAuth(auth); setActive(isStaff(auth.user)?'dashboard':'citizen');
    }).catch(e => setOauthError('Google login could not complete: ' + e.message))
      .finally(() => setOauthBusy(false));
  }, []);
  useEffect(()=>{const clear=()=>{setAuth(null);setActive('citizen')};window.addEventListener('crisisaid-expired',clear);return()=>window.removeEventListener('crisisaid-expired',clear)},[]);
  if(!session)return <>{oauthBusy && <div className="oauth-progress">Completing Google sign-in…</div>}{oauthError && <div className="oauth-error" role="alert">{oauthError}</div>}<Auth onLogin={s=>{setAuth(s);setActive(isStaff(s.user)?'dashboard':'citizen')}}/></>;
  const user=session.user;
  const nav=allNav.filter(x=>!x.staff||isStaff(user)).filter(x=>!x.roles||x.roles.includes(user.role));
  const go=key=>{setActive(key);setMenu(false);window.scrollTo?.(0,0)};
  const logout=()=>{setSession(null);setAuth(null)};
  const views={alerts:<Alerts user={user}/>,household:<Household/>,feedback:<Feedback/>,partners:<Partners user={user}/>,decision:<DecisionLab/>,crises:<Crises user={user}/>,analytics:<Analytics/>,account:<Account/>,guide:<Guide/>,dashboard:<Dashboard onNavigate={go}/>,requests:<Requests/>,citizen:<Citizen/>,resources:<Resources user={user}/>,shelters:<Shelters user={user}/>,volunteers:<Volunteers user={user}/>,donations:<Donations user={user}/>,map:<MapPage/>,admin:<Admin user={user}/>,notifications:<Notifications/>};
  return <div className="app-shell"><aside className={`sidebar ${menu?'open':''}`}><div className="sidebar-top"><div className="logo-line"><div className="brand-mark"><HeartHandshake size={21}/></div><strong>Crisis<span>Aid</span></strong></div><button className="mobile-close" onClick={()=>setMenu(false)} aria-label="Close menu"><X size={21}/></button></div><div className="sidebar-context"><div className="context-icon"><RadioTower size={20}/></div><div><span>OPERATIONS</span><strong>Humanitarian Response</strong><small>Multi-organisation platform</small></div></div><div className="nav-header">WORKSPACE</div><nav aria-label="Main navigation">{nav.map(x=><button className={`nav-item ${active===x.key?'selected':''}`} key={x.key} onClick={()=>go(x.key)}><x.icon size={19}/><span>{x.label}</span>{x.key==='notifications'&&<span className="nav-dot"/>}</button>)}</nav><div className="sidebar-foot"><div className="security-label"><ShieldCheck size={16}/> Protected by role-based access</div><div className="sidebar-account"><div className="avatar">{user.displayName?.slice(0,1)||'U'}</div><div><strong>{user.displayName}</strong><small>{user.role.replace(/([a-z])([A-Z])/g,'$1 $2')}</small></div><button onClick={logout} title="Sign out" aria-label="Sign out"><LogOut size={18}/></button></div></div></aside>{menu&&<button className="mobile-overlay" onClick={()=>setMenu(false)} aria-label="Close navigation"/>}<div className="main-shell"><header className="topbar"><button className="mobile-menu" onClick={()=>setMenu(true)} aria-label="Open navigation"><Menu size={22}/></button><div><span className="topbar-caption">RESPONSE COMMAND CENTER</span><div className="breadcrumb">Workspace <span>/</span> <strong>{allNav.find(x=>x.key===active)?.label||'Overview'}</strong></div></div><div className="topbar-actions"><span className="env-tag"><i/> SYSTEM ACTIVE</span><button onClick={()=>go('notifications')} aria-label="Notifications" className="topbar-icon"><Bell size={19}/></button><div className="topbar-avatar">{user.displayName?.slice(0,1)||'U'}</div></div></header><main className="content">{views[active]||views.citizen}<footer className="app-footer">CrisisAid · SE205.3 Coursework Prototype · Demo data is fictional · Not an emergency response service</footer></main></div></div>;
}
