import { useState } from 'react';
import { HeartHandshake, ShieldCheck, ArrowRight, CircleHelp } from 'lucide-react';
import { post, setSession } from '../api';
import { Button, ErrorMessage, Field } from '../components/UI';
export default function Auth({ onLogin }) {
  const [register, setRegister] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState('');
  const [form, setForm] = useState({ name: '', email: 'coordinator@demo.local', password: '', role: 'Citizen' });
  const set = (k, v) => setForm(s => ({ ...s, [k]: v }));
  const submit = async e => {
    e.preventDefault(); setBusy(true); setError('');
    try {
      if (register) { await post('/auth/register', form); setRegister(false); setError('Registration complete. Sign in to continue.'); }
      else { const session = await post('/auth/login', form); setSession(session); onLogin(session); }
    } catch (e) { setError(e.message); } finally { setBusy(false); }
  };
  return <div className="auth-page"><div className="auth-art"><div className="brand-mark large"><HeartHandshake size={28}/></div><div className="eyebrow light">HUMANITARIAN OPERATIONS PLATFORM</div><h1>When every minute matters, coordination matters more.</h1><p>One shared command center for people, resources and responsible decisions across humanitarian emergencies.</p><div className="auth-proof"><ShieldCheck size={22}/> Human-reviewed AI recommendations · Auditable decisions · Connected response teams</div><div className="auth-motif" /></div><div className="auth-main"><div className="auth-inner"><div className="logo-line"><span className="brand-mark"><HeartHandshake size={21}/></span><strong>Crisis<span>Aid</span></strong></div><p className="eyebrow">SECURE PORTAL</p><h2>{register ? 'Create an account' : 'Welcome back'}</h2><p className="muted">{register ? 'Register as an affected resident, volunteer or donor.' : 'Sign in to continue coordinating assistance.'}</p><form onSubmit={submit} className="auth-form">{register && <Field label="Your name"><input required value={form.name} onChange={e=>set('name',e.target.value)}/></Field>}<Field label="Email address"><input type="email" required value={form.email} onChange={e=>set('email',e.target.value)}/></Field><Field label="Password"><input type="password" minLength={register ? 10 : undefined} required value={form.password} onChange={e=>set('password',e.target.value)}/></Field>{register && <Field label="I am registering as"><select value={form.role} onChange={e=>set('role',e.target.value)}><option value="Citizen">Affected resident</option><option value="Volunteer">Volunteer</option><option value="Donor">Donor</option></select></Field>}<ErrorMessage error={error}/><Button type="submit" disabled={busy}>{busy ? 'Processing...' : register ? 'Create account' : 'Sign in'} <ArrowRight size={16}/></Button></form><p className="auth-switch">{register ? 'Already have an account?' : 'Need an account?'} <button onClick={() => { setRegister(!register); setError(''); }}>{register ? 'Sign in' : 'Register'}</button></p><div className="auth-note"><CircleHelp size={16}/> For a local demo, use credentials from your configured <code>.env</code> file. This system is a coursework prototype, not a live emergency service.</div></div></div></div>;
}
