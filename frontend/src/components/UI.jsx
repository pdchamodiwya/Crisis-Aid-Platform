import React from 'react';
import { AlertTriangle, CheckCircle2, LoaderCircle, Info } from 'lucide-react';
export function SectionHeading({ kicker, title, subtitle, action }) { return <div className="section-heading"><div>{kicker && <div className="eyebrow">{kicker}</div>}<h2>{title}</h2>{subtitle && <p>{subtitle}</p>}</div>{action && <div className="heading-action">{action}</div>}</div>; }
export function Panel({ title, subtitle, action, children, className = '' }) { return <section className={`panel ${className}`}><div className="panel-heading"><div><h3>{title}</h3>{subtitle && <p>{subtitle}</p>}</div>{action}</div>{children}</section>; }
export function Stat({ icon: Icon, label, value, detail, tone = 'navy' }) { return <div className="stat-card"><div className={`stat-icon ${tone}`}><Icon size={20}/></div><div className="stat-label">{label}</div><div className="stat-value">{value}</div><div className="stat-detail">{detail}</div></div>; }
export function Pill({ children, tone }) { let variant = tone || (/critical|rejected|failed|escalat/i.test(children) ? 'red' : /high|pending|submitted|deferred|reserved/i.test(children) ? 'amber' : /verified|approved|fulfilled|completed|open|sent|accepted/i.test(children) ? 'green' : 'blue'); return <span className={`pill ${variant}`}>{children}</span>; }
export function Empty({ text = 'No records to display.' }) { return <div className="empty"><Info size={22}/><p>{text}</p></div>; }
export function ErrorMessage({ error }) { return error ? <div className="feedback error"><AlertTriangle size={16}/><span>{error}</span></div> : null; }
export function Success({ message }) { return message ? <div className="feedback success"><CheckCircle2 size={16}/>{message}</div> : null; }
export function Loading() { return <div className="loading"><LoaderCircle className="spin"/>Loading data...</div>; }
export function Field({ label, children, hint }) { return <label className="field"><span>{label}</span>{children}{hint && <small>{hint}</small>}</label>; }
export function Button({ children, onClick, variant = 'primary', disabled, type = 'button' }) { return <button type={type} className={`btn ${variant}`} onClick={onClick} disabled={disabled}>{children}</button>; }
export function Progress({ value, max = 100 }) { const n = Math.max(0, Math.min(100, max ? 100 * value / max : 0)); return <div className="progress" role="progressbar" aria-valuenow={value} aria-valuemin="0" aria-valuemax={max}><span style={{ width: `${n}%` }} /></div>; }
export function SafeTable({ headers, children, empty }) { return <div className="table-scroll"><table><thead><tr>{headers.map(h => <th key={h}>{h}</th>)}</tr></thead><tbody>{children || <tr><td colSpan={headers.length}><Empty text={empty}/></td></tr>}</tbody></table></div>; }
