import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HeartHandshake, ShieldCheck, ArrowRight, CircleHelp } from 'lucide-react';
import { post, setSession } from '../api';
import { Button, ErrorMessage, Field } from '../components/UI';
import LanguageSwitcher from '../components/LanguageSwitcher';

export default function Auth({ onLogin }) {
  const { t } = useTranslation();
  const [register, setRegister] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [form, setForm] = useState({ name: '', email: 'coordinator@demo.local', password: '', role: 'Citizen' });
  const set = (k, v) => setForm(s => ({ ...s, [k]: v }));

  const submit = async e => {
    e.preventDefault(); setBusy(true); setError('');
    try {
      if (register) {
        await post('/auth/register', form);
        setRegister(false);
        setError(t('registrationComplete'));
      } else {
        const session = await post('/auth/login', form);
        setSession(session);
        onLogin(session);
      }
    } catch (e) { setError(e.message); } finally { setBusy(false); }
  };

  return (
    <div className="auth-page">
      <div className="auth-art">
        <div className="brand-mark large"><HeartHandshake size={28}/></div>
        <div className="eyebrow light">{t('eyebrow')}</div>
        <h1>{t('heroTitle')}</h1>
        <p>{t('heroText')}</p>
        <div className="auth-proof"><ShieldCheck size={22}/> {t('proof')}</div>
        <div className="auth-motif" />
      </div>

      <div className="auth-main">
        <div className="auth-inner">
          <div style={{ textAlign: 'right', marginBottom: 12 }}><LanguageSwitcher /></div>
          <div className="logo-line">
            <span className="brand-mark"><HeartHandshake size={21}/></span>
            <strong>Crisis<span>Aid</span></strong>
          </div>
          <p className="eyebrow">{t('securePortal')}</p>
          <h2>{register ? t('createAccount') : t('welcomeBack')}</h2>
          <p className="muted">{register ? t('registerSub') : t('signInContinue')}</p>

          <form onSubmit={submit} className="auth-form">
            {register && (
              <Field label={t('yourName')}>
                <input required value={form.name} onChange={e => set('name', e.target.value)} />
              </Field>
            )}
            <Field label={t('email')}>
              <input type="email" required value={form.email} onChange={e => set('email', e.target.value)} />
            </Field>
            <Field label={t('password')}>
              <input type="password" minLength={register ? 10 : undefined} required value={form.password} onChange={e => set('password', e.target.value)} />
            </Field>
            {register && (
              <Field label={t('registeringAs')}>
                <select value={form.role} onChange={e => set('role', e.target.value)}>
                  <option value="Citizen">{t('affectedResident')}</option>
                  <option value="Volunteer">{t('volunteer')}</option>
                  <option value="Donor">{t('donor')}</option>
                </select>
              </Field>
            )}
            <ErrorMessage error={error} />
            <Button type="submit" disabled={busy}>
              {busy ? t('processing') : register ? t('createAccount') : t('signIn')} <ArrowRight size={16}/>
            </Button>
          </form>

          <p className="auth-switch">
            {register ? t('haveAccount') : t('needAccount')}{' '}
            <button onClick={() => { setRegister(!register); setError(''); }}>
              {register ? t('signIn') : t('register')}
            </button>
          </p>

          <div className="auth-note">
            <CircleHelp size={16}/> {t('demoNote1')} <code>.env</code> {t('demoNote2')}
          </div>
        </div>
      </div>
    </div>
  );
}