import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HeartHandshake, LayoutDashboard, ClipboardList, HandHeart, Boxes, House, UsersRound, HandCoins, MapPinned, ShieldCheck, Bell, LogOut, Menu, X, RadioTower, AlertTriangle, ChartNoAxesCombined, UserRoundCog, BookOpen, Megaphone, Building2, MessageSquareHeart, BrainCircuit, Home } from 'lucide-react';
import { getSession, setSession, can, isStaff, post } from './api';
import LanguageSwitcher from './components/LanguageSwitcher';
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
  {key:'dashboard',icon:LayoutDashboard,staff:true},
  {key:'crises',icon:AlertTriangle},
  {key:'alerts',icon:Megaphone},
  {key:'partners',icon:Building2,roles:['EmergencyCoordinator','PartnerOfficer','LogisticsManager','SystemAdministrator']},
  {key:'decision',icon:BrainCircuit,roles:['EmergencyCoordinator','CaseManager','LogisticsManager','VolunteerCoordinator','SystemAdministrator']},
  {key:'analytics',icon:ChartNoAxesCombined,staff:true},
  {key:'requests',icon:ClipboardList,staff:true},
  {key:'citizen',icon:HandHeart},
  {key:'household',icon:Home},
  {key:'feedback',icon:MessageSquareHeart},
  {key:'resources',icon:Boxes,staff:true},
  {key:'shelters',icon:House},
  {key:'volunteers',icon:UsersRound},
  {key:'donations',icon:HandCoins},
  {key:'map',icon:MapPinned},
  {key:'notifications',icon:Bell},
  {key:'admin',icon:ShieldCheck,roles:['SystemAdministrator','EmergencyCoordinator']},
  {key:'account',icon:UserRoundCog},
  {key:'guide',icon:BookOpen}
];

export default function App() {
  const { t } = useTranslation();
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
    }).catch(e => setOauthError(t('googleFailed') + e.message))
      .finally(() => setOauthBusy(false));
  }, []);
  useEffect(()=>{const clear=()=>{setAuth(null);setActive('citizen')};window.addEventListener('crisisaid-expired',clear);return()=>window.removeEventListener('crisisaid-expired',clear)},[]);
  if(!session)return <>{oauthBusy && <div className="oauth-progress">{t('googleCompleting')}</div>}{oauthError && <div className="oauth-error" role="alert">{oauthError}</div>}<Auth onLogin={s=>{setAuth(s);setActive(isStaff(s.user)?'dashboard':'citizen')}}/></>;
  const user=session.user;
  const nav=allNav.filter(x=>!x.staff||isStaff(user)).filter(x=>!x.roles||x.roles.includes(user.role));
  const go=key=>{setActive(key);setMenu(false);window.scrollTo?.(0,0)};
  const logout=()=>{setSession(null);setAuth(null)};
  const views={alerts:<Alerts user={user}/>,household:<Household/>,feedback:<Feedback/>,partners:<Partners user={user}/>,decision:<DecisionLab/>,crises:<Crises user={user}/>,analytics:<Analytics/>,account:<Account/>,guide:<Guide/>,dashboard:<Dashboard onNavigate={go}/>,requests:<Requests/>,citizen:<Citizen/>,resources:<Resources user={user}/>,shelters:<Shelters user={user}/>,volunteers:<Volunteers user={user}/>,donations:<Donations user={user}/>,map:<MapPage user={user}/>,admin:<Admin user={user}/>,notifications:<Notifications/>};
  const currentKey = allNav.find(x=>x.key===active)?.key || 'dashboard';
  return <div className="app-shell"><aside className={`sidebar ${menu?'open':''}`}><div className="sidebar-top"><div className="logo-line"><div className="brand-mark"><HeartHandshake size={21}/></div><strong>Crisis<span>Aid</span></strong></div><button className="mobile-close" onClick={()=>setMenu(false)} aria-label={t('closeMenu')}><X size={21}/></button></div><div className="sidebar-context"><div className="context-icon"><RadioTower size={20}/></div><div><span>{t('operations')}</span><strong>{t('humanitarianResponse')}</strong><small>{t('multiOrg')}</small></div></div><div className="nav-header">{t('workspace')}</div><nav aria-label={t('mainNavigation')}>{nav.map(x=><button className={`nav-item ${active===x.key?'selected':''}`} key={x.key} onClick={()=>go(x.key)}><x.icon size={19}/><span>{t('nav_'+x.key)}</span>{x.key==='notifications'&&<span className="nav-dot"/>}</button>)}</nav><div className="sidebar-foot"><div className="security-label"><ShieldCheck size={16}/> {t('protectedRbac')}</div><div className="sidebar-account"><div className="avatar">{user.displayName?.slice(0,1)||'U'}</div><div><strong>{user.displayName}</strong><small>{user.role.replace(/([a-z])([A-Z])/g,'$1 $2')}</small></div><button onClick={logout} title={t('signOut')} aria-label={t('signOut')}><LogOut size={18}/></button></div></div></aside>{menu&&<button className="mobile-overlay" onClick={()=>setMenu(false)} aria-label={t('closeNavigation')}/>}<div className="main-shell"><header className="topbar"><button className="mobile-menu" onClick={()=>setMenu(true)} aria-label={t('openNavigation')}><Menu size={22}/></button><div><span className="topbar-caption">{t('commandCenter')}</span><div className="breadcrumb">{t('breadcrumbWorkspace')} <span>/</span> <strong>{t('nav_'+currentKey)}</strong></div></div><div className="topbar-actions"><LanguageSwitcher /><span className="env-tag"><i/> {t('systemActive')}</span><button onClick={()=>go('notifications')} aria-label={t('nav_notifications')} className="topbar-icon"><Bell size={19}/></button><div className="topbar-avatar">{user.displayName?.slice(0,1)||'U'}</div></div></header><main className="content">{views[active]||views.citizen}<footer className="app-footer">{t('footer')}</footer></main></div></div>;
}