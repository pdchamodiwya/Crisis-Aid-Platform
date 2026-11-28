const BASE = import.meta.env.VITE_API_BASE || '/api';
const STORAGE = 'crisisaid-session';
export const getSession = () => { try { return JSON.parse(sessionStorage.getItem(STORAGE) || 'null'); } catch { return null; } };
export const setSession = value => value ? sessionStorage.setItem(STORAGE, JSON.stringify(value)) : sessionStorage.removeItem(STORAGE);
export async function api(path, options = {}) {
  const token = getSession()?.token;
  const response = await fetch(BASE + path, {
    ...options,
    headers: { ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(options.headers || {}) }
  });
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try { const detail = await response.json(); message = detail.error || detail.title || detail.message || message; } catch { /* empty error body */ }
    if (response.status === 401) { setSession(null); window.dispatchEvent(new Event('crisisaid-expired')); }
    throw new Error(message);
  }
  if (response.status === 204 || response.status === 202) return null;
  return response.json();
}
export const get = (path) => api(path);
export const post = (path, obj) => api(path, { method: 'POST', body: JSON.stringify(obj) });
export const patch = (path, obj) => api(path, { method: 'PATCH', body: JSON.stringify(obj) });
export const put = (path, obj) => api(path, { method: 'PUT', body: JSON.stringify(obj) });
export const del = (path) => api(path, { method: 'DELETE' });
export function formatDate(value) { return value ? new Date(value).toLocaleString() : '-'; }
export function shortId(id) { return id ? `#${id.slice(0, 8).toUpperCase()}` : '-'; }
export const STAFF = ['SystemAdministrator', 'EmergencyCoordinator', 'CaseManager', 'LogisticsManager', 'ShelterAdministrator', 'VolunteerCoordinator', 'FundingOfficer', 'PartnerOfficer'];
export const isStaff = user => STAFF.includes(user?.role);
export const can = (user, ...roles) => roles.includes(user?.role);
