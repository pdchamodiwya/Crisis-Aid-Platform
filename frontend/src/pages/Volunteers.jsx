import { useEffect, useState } from 'react';
import { HeartHandshake, UserCheck } from 'lucide-react';
import { api, get, post, put, patch, shortId } from '../api';
import { SectionHeading, Panel, Field, Button, Pill, ErrorMessage, Success, Empty, SafeTable } from '../components/UI';

const coordinatorRoles = ['VolunteerCoordinator', 'EmergencyCoordinator', 'SystemAdministrator'];
const managedSkillOptions = ['First aid', 'Translation', 'Transportation', 'Distribution', 'Administration'];

const skillsForDisplay = volunteer => {
  const skills = new Map();
  (volunteer.skills || '').split(',').map(name => name.trim()).filter(Boolean).forEach(name => {
    skills.set(name.toLocaleLowerCase(), { id: `legacy-${name.toLocaleLowerCase()}`, name, verified: false, legacy: true });
  });
  (volunteer.structuredSkills || []).forEach(skill => {
    skills.set(skill.name.toLocaleLowerCase(), { ...skill, legacy: false });
  });
  return [...skills.values()].sort((a, b) => a.name.localeCompare(b.name));
};

export default function Volunteers({ user }) {
  const [people, setPeople] = useState([]);
  const [tasks, setTasks] = useState([]);
  const [requests, setRequests] = useState([]);
  const [skills, setSkills] = useState([]);
  const [availability, setAvailability] = useState([]);
  const [qualifications, setQualifications] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [safetyStatus, setSafetyStatus] = useState([]);
  const [matches, setMatches] = useState([]);
  const [findingMatches, setFindingMatches] = useState(false);
  const [hasSearchedMatches, setHasSearchedMatches] = useState(false);
  const [profile, setProfile] = useState({ skills: 'First aid', area: 'Colombo', available: true, latitude: '', longitude: '' });
  const [assignment, setAssignment] = useState({
    volunteerId: '', requestId: '', requiredSkill: 'First aid', description: '',
    scheduledStartAt: '', durationMinutes: 120, urgency: 'Medium', latitude: '', longitude: '', checkInIntervalMinutes: 30
  });
  const [availabilityForm, setAvailabilityForm] = useState({ startsAt: '', endsAt: '' });
  const [qualificationForm, setQualificationForm] = useState({ name: '', issuer: '', reference: '', expiresAt: '' });
  const [selectedVolunteerId, setSelectedVolunteerId] = useState('');
  const [managedSkill, setManagedSkill] = useState('');
  const [managedQualification, setManagedQualification] = useState({ name: '', issuer: '', expiresAt: '' });
  const [declineReason, setDeclineReason] = useState('');
  const [completion, setCompletion] = useState({ hoursWorked: '', notes: '' });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const manage = coordinatorRoles.includes(user.role);
  const isVolunteer = user.role === 'Volunteer';

  const load = async () => {
    const [taskList, requestList] = await Promise.all([get('/volunteers/tasks'), get('/requests')]);
    setTasks(taskList);
    setRequests(requestList.filter(r => ['Verified', 'Assigned'].includes(r.status)));
    if (isVolunteer) {
      const [skillList, windows, quals, currentProfile] = await Promise.all([
        get('/volunteers/me/skills'), get('/volunteers/me/availability'), get('/volunteers/me/qualifications'),
        get('/volunteers/me/profile')
      ]);
      setSkills(skillList);
      setAvailability(windows);
      setQualifications(quals);
      if (currentProfile) setProfile({
        ...currentProfile,
        latitude: currentProfile.latitude ?? '',
        longitude: currentProfile.longitude ?? ''
      });
    }
    if (manage) {
      const [registered, overdue, currentSafety] = await Promise.all([
        get('/volunteers'), get('/volunteers/check-ins'), get('/volunteers/safety-status')
      ]);
      const enriched = await Promise.all(registered.map(async p => ({
        ...p,
        structuredSkills: await get(`/volunteers/${p.id}/skills`),
        qualifications: await get(`/volunteers/${p.id}/qualifications`)
      })));
      setPeople(enriched);
      setAlerts(overdue.filter(x => x.status === 'Overdue'));
      setSafetyStatus(currentSafety);
    }
  };
  useEffect(() => { load().catch(e => setError(e.message)); }, []);

  const act = async (fn, message) => {
    try {
      await fn();
      await load();
      setSuccess(message);
      setError('');
    } catch (e) { setError(e.message); }
  };

  const saveProfile = () => act(async () => {
    await put('/volunteers/me', {
      ...profile,
      latitude: profile.latitude === '' ? null : Number(profile.latitude),
      longitude: profile.longitude === '' ? null : Number(profile.longitude)
    });
    await put('/volunteers/me/skills', {
      skills: profile.skills.split(',').map(x => x.trim()).filter(Boolean)
    });
  }, 'Volunteer profile and structured skills saved.');

  const addAvailability = () => act(async () => {
    await post('/volunteers/me/availability', {
      startsAt: new Date(availabilityForm.startsAt).toISOString(),
      endsAt: new Date(availabilityForm.endsAt).toISOString()
    });
    setAvailabilityForm({ startsAt: '', endsAt: '' });
  }, 'Availability window added.');

  const submitQualification = () => act(async () => {
    await post('/volunteers/me/qualifications', {
      ...qualificationForm,
      expiresAt: qualificationForm.expiresAt ? new Date(qualificationForm.expiresAt).toISOString() : null
    });
    setQualificationForm({ name: '', issuer: '', reference: '', expiresAt: '' });
  }, 'Qualification submitted for coordinator verification.');

  const addManagedSkill = () => {
    if (!selectedVolunteerId || !managedSkillOptions.includes(managedSkill)) {
      setError('Select a volunteer and a valid skill.');
      return;
    }
    act(async () => {
      await post(`/volunteers/${selectedVolunteerId}/skills`, { skill: managedSkill });
      setManagedSkill('');
    },
      'Skill added as unverified.');
  };

  const addManagedQualification = () => {
    const name = managedQualification.name.trim();
    const issuer = managedQualification.issuer.trim();
    const expiresAt = managedQualification.expiresAt;
    if (!selectedVolunteerId || !name || name.length > 120 || !issuer || issuer.length > 120 ||
        (expiresAt && (!Number.isFinite(Date.parse(expiresAt)) || expiresAt < new Date().toISOString().slice(0, 10)))) {
      setError('Select a volunteer, provide a valid qualification name and issuer, and use an expiry date that is not in the past.');
      return;
    }
    act(async () => {
      await post(`/volunteers/${selectedVolunteerId}/qualifications`, {
        name, issuer, expiryDate: expiresAt || null
      });
      setManagedQualification({ name: '', issuer: '', expiresAt: '' });
    }, 'Qualification added as unverified.');
  };

  const findMatches = async () => {
    setFindingMatches(true);
    setHasSearchedMatches(false);
    setMatches([]);
    setError('');
    try {
      const result = await post('/volunteers/matches', {
        requiredSkill: assignment.requiredSkill,
        scheduledStartAt: assignment.scheduledStartAt ? new Date(assignment.scheduledStartAt).toISOString() : null,
        durationMinutes: assignment.scheduledStartAt ? Number(assignment.durationMinutes) : null,
        urgency: assignment.urgency,
        latitude: assignment.latitude === '' ? null : Number(assignment.latitude),
        longitude: assignment.longitude === '' ? null : Number(assignment.longitude)
      });
      if (!Array.isArray(result.candidates)) throw new Error('Candidate response did not contain a list.');
      setMatches(result.candidates);
      setHasSearchedMatches(true);
    } catch (e) {
      setError(e.message);
    } finally {
      setFindingMatches(false);
    }
  };

  const assign = volunteerId => act(() => post('/volunteers/tasks', {
    ...assignment,
    volunteerId,
    requestId: assignment.requestId || null,
    scheduledStartAt: assignment.scheduledStartAt ? new Date(assignment.scheduledStartAt).toISOString() : null,
    durationMinutes: assignment.scheduledStartAt ? Number(assignment.durationMinutes) : null,
    latitude: assignment.latitude === '' ? null : Number(assignment.latitude),
    longitude: assignment.longitude === '' ? null : Number(assignment.longitude),
    checkInIntervalMinutes: Number(assignment.checkInIntervalMinutes)
  }), 'Volunteer task assigned and notification queued.');

  return <>
    <SectionHeading kicker="FIELD OPERATIONS / PEOPLE" title="Volunteer coordination" subtitle="Verify structured skills, schedule safe assignments and audit task history" />
    <ErrorMessage error={error} />
    <Success message={success} />

    {isVolunteer && <Panel title="My volunteer profile" subtitle="Share skills and area; precise profile coordinates are optional">
      <div className="inline-form">
        <Field label="Skills (comma-separated)"><input value={profile.skills} onChange={e => setProfile(s => ({ ...s, skills: e.target.value }))} /></Field>
        <Field label="Operating area"><input value={profile.area} onChange={e => setProfile(s => ({ ...s, area: e.target.value }))} /></Field>
        <Field label="Latitude (optional)"><input type="number" step="any" value={profile.latitude} onChange={e => setProfile(s => ({ ...s, latitude: e.target.value }))} /></Field>
        <Field label="Longitude (optional)"><input type="number" step="any" value={profile.longitude} onChange={e => setProfile(s => ({ ...s, longitude: e.target.value }))} /></Field>
        <label className="check-row"><input type="checkbox" checked={profile.available} onChange={e => setProfile(s => ({ ...s, available: e.target.checked }))} /> Currently available</label>
        <Button onClick={saveProfile}><UserCheck size={16} /> Save profile</Button>
      </div>
      <SafeTable headers={['SKILL', 'VERIFICATION']} empty="Save your profile to add structured skills.">
        {skills.map(s => <tr key={s.id}><td>{s.name}</td><td><Pill>{s.verified ? 'Verified' : 'Pending'}</Pill></td></tr>)}
      </SafeTable>
    </Panel>}

    {isVolunteer && <Panel title="Availability calendar" subtitle="Add a future date/time window. Overlapping windows and conflicting assignments are rejected.">
      <div className="inline-form">
        <Field label="Available from"><input type="datetime-local" value={availabilityForm.startsAt} onChange={e => setAvailabilityForm(s => ({ ...s, startsAt: e.target.value }))} /></Field>
        <Field label="Available until"><input type="datetime-local" value={availabilityForm.endsAt} onChange={e => setAvailabilityForm(s => ({ ...s, endsAt: e.target.value }))} /></Field>
        <Button disabled={!availabilityForm.startsAt || !availabilityForm.endsAt} onClick={addAvailability}>Add availability</Button>
      </div>
      <SafeTable headers={['FROM', 'UNTIL', 'ACTION']} empty="No availability windows recorded.">
        {availability.map(a => <tr key={a.id}><td>{new Date(a.startsAt).toLocaleString()}</td><td>{new Date(a.endsAt).toLocaleString()}</td>
          <td><Button variant="outline" onClick={() => act(() => api(`/volunteers/me/availability/${a.id}`, { method: 'DELETE' }), 'Availability removed.')}>Remove</Button></td></tr>)}
      </SafeTable>
    </Panel>}

    {isVolunteer && <Panel title="Qualifications" subtitle="Provide relevant qualifications for coordinator verification">
      <div className="inline-form">
        <Field label="Qualification"><input value={qualificationForm.name} onChange={e => setQualificationForm(s => ({ ...s, name: e.target.value }))} /></Field>
        <Field label="Issuer"><input value={qualificationForm.issuer} onChange={e => setQualificationForm(s => ({ ...s, issuer: e.target.value }))} /></Field>
        <Field label="Reference"><input value={qualificationForm.reference} onChange={e => setQualificationForm(s => ({ ...s, reference: e.target.value }))} /></Field>
        <Field label="Expires (optional)"><input type="date" value={qualificationForm.expiresAt} onChange={e => setQualificationForm(s => ({ ...s, expiresAt: e.target.value }))} /></Field>
        <Button disabled={!qualificationForm.name} onClick={submitQualification}>Submit qualification</Button>
      </div>
      <SafeTable headers={['QUALIFICATION', 'ISSUER', 'EXPIRY', 'STATUS']} empty="No qualifications submitted.">
        {qualifications.map(q => <tr key={q.id}><td>{q.name}</td><td>{q.issuer || '-'}</td><td>{q.expiresAt ? new Date(q.expiresAt).toLocaleDateString() : 'No expiry'}</td><td><Pill>{q.verified ? 'Verified' : 'Pending'}</Pill></td></tr>)}
      </SafeTable>
    </Panel>}

    {manage && <>
      <Panel title="Add volunteer skill or qualification" subtitle="Select a volunteer; new records remain unverified until reviewed">
        <div className="inline-form">
          <Field label="Volunteer">
            <select value={selectedVolunteerId} onChange={e => setSelectedVolunteerId(e.target.value)}>
              <option value="">Select a volunteer</option>
              {people.map(p => <option key={p.id} value={p.id}>{shortId(p.userId)} · {p.area}</option>)}
            </select>
          </Field>
          <Field label="Skill">
            <select value={managedSkill} onChange={e => setManagedSkill(e.target.value)}>
              <option value="">Select a skill</option>
              {managedSkillOptions.map(skill => <option key={skill}>{skill}</option>)}
            </select>
          </Field>
          <Button disabled={!selectedVolunteerId || !managedSkill} onClick={addManagedSkill}>Add skill</Button>
          <Field label="Qualification name">
            <input maxLength="120" value={managedQualification.name}
              onChange={e => setManagedQualification(s => ({ ...s, name: e.target.value }))} />
          </Field>
          <Field label="Issuer">
            <input maxLength="120" value={managedQualification.issuer}
              onChange={e => setManagedQualification(s => ({ ...s, issuer: e.target.value }))} />
          </Field>
          <Field label="Expiry date (optional)">
            <input type="date" value={managedQualification.expiresAt}
              onChange={e => setManagedQualification(s => ({ ...s, expiresAt: e.target.value }))} />
          </Field>
          <Button disabled={!selectedVolunteerId || !managedQualification.name.trim() || !managedQualification.issuer.trim()}
            onClick={addManagedQualification}>Add qualification</Button>
        </div>
      </Panel>

      <Panel title="Registered volunteers" subtitle="Verify skills and qualifications before assigning suitable tasks">
        <SafeTable headers={['VOLUNTEER', 'AVAILABILITY', 'PROFILE VERIFIED', 'SKILLS', 'QUALIFICATIONS']} empty="No registered volunteers.">
          {people.map(p => <tr key={p.id}><td><strong>{shortId(p.userId)}</strong><br />{p.area}</td><td><Pill>{p.available ? 'Available' : 'Busy'}</Pill></td>
            <td><Pill>{p.verified ? 'Verified' : 'Pending'}</Pill>
              <Button variant="outline" onClick={() => act(() => patch(`/volunteers/${p.id}/verify`, { verified: !p.verified }), 'Volunteer verification updated.')}>{p.verified ? 'Revoke' : 'Verify'}</Button>
            </td>
            <td>{skillsForDisplay(p).length ? skillsForDisplay(p).map(s => <div key={s.id}>
              <Pill>{s.name}</Pill> <Pill tone={s.verified ? 'green' : 'amber'}>{s.verified ? 'Verified' : 'Unverified'}</Pill>
              {s.legacy && <small className="muted">Legacy profile entry</small>}
              {!s.legacy && <Button variant="outline" onClick={() => act(() => patch(`/volunteer-skills/${s.id}/verify`, { verified: !s.verified }), 'Skill verification updated.')}>{s.verified ? 'Revoke' : 'Verify'}</Button>}
            </div>) : <span className="muted">No skills recorded</span>}</td>
            <td>{p.qualifications.length ? p.qualifications.map(q => <div key={q.id}>
              <Pill>{q.name}</Pill> <span className="muted">{q.issuer || 'Issuer not listed'}</span>
              <Pill tone={q.verified ? 'green' : 'amber'}>{q.verified ? 'Verified' : 'Unverified'}</Pill>
              {q.expiresAt && <small className="muted">Expires {new Date(q.expiresAt).toLocaleDateString()}</small>}
              <Button variant="outline" onClick={() => act(() => patch(`/volunteer-qualifications/${q.id}/verify`, { verified: !q.verified }), 'Qualification verification updated.')}>{q.verified ? 'Revoke' : 'Verify'}</Button>
            </div>) : <span className="muted">No qualifications recorded</span>}</td>
          </tr>)}
        </SafeTable>
      </Panel>

      <Panel title="Assign a field task" subtitle="Candidates are ranked by verified skills, qualifications, availability, distance and workload">
        <div className="inline-form">
          <Field label="Required skill"><input value={assignment.requiredSkill} onChange={e => setAssignment(s => ({ ...s, requiredSkill: e.target.value }))} /></Field>
          <Field label="Approved request (optional)"><select value={assignment.requestId} onChange={e => setAssignment(s => ({ ...s, requestId: e.target.value }))}>
            <option value="">General task</option>{requests.map(r => <option key={r.id} value={r.id}>{shortId(r.id)} · {r.category}</option>)}
          </select></Field>
          <Field label="Instructions"><input value={assignment.description} onChange={e => setAssignment(s => ({ ...s, description: e.target.value }))} /></Field>
          <Field label="Scheduled start (optional)"><input type="datetime-local" value={assignment.scheduledStartAt} onChange={e => setAssignment(s => ({ ...s, scheduledStartAt: e.target.value }))} /></Field>
          <Field label="Duration (minutes)"><input type="number" min="1" value={assignment.durationMinutes} onChange={e => setAssignment(s => ({ ...s, durationMinutes: e.target.value }))} /></Field>
          <Field label="Urgency"><select value={assignment.urgency} onChange={e => setAssignment(s => ({ ...s, urgency: e.target.value }))}><option>Low</option><option>Medium</option><option>High</option><option>Critical</option></select></Field>
          <Field label="Task latitude (optional)"><input type="number" step="any" value={assignment.latitude} onChange={e => setAssignment(s => ({ ...s, latitude: e.target.value }))} /></Field>
          <Field label="Task longitude (optional)"><input type="number" step="any" value={assignment.longitude} onChange={e => setAssignment(s => ({ ...s, longitude: e.target.value }))} /></Field>
          <Field label="Check-in interval (minutes)"><input type="number" min="1" value={assignment.checkInIntervalMinutes} onChange={e => setAssignment(s => ({ ...s, checkInIntervalMinutes: e.target.value }))} /></Field>
          <Button disabled={!assignment.requiredSkill || !assignment.description || findingMatches} onClick={findMatches}>
            {findingMatches ? 'Finding candidates...' : 'Find ranked candidates'}
          </Button>
        </div>
        {findingMatches && <p role="status" aria-live="polite">Finding ranked candidates...</p>}
        {hasSearchedMatches && matches.length === 0 && !findingMatches &&
          <Empty text="No suitable verified volunteer found for this skill. Check that the skill is verified and the volunteer is available." />}
        {matches.length > 0 && <SafeTable headers={['CANDIDATE', 'SCORE', 'REASONS', 'ACTION']}>
          {matches.map(m => <tr key={m.volunteerId}><td>{shortId(m.userId)} · {m.area}</td><td>{m.score}</td><td>{m.reasons.join(' · ')}</td>
            <td><Button disabled={!m.isEligible} onClick={() => assign(m.volunteerId)}>Assign</Button></td></tr>)}
        </SafeTable>}
      </Panel>

      <Panel title="Overdue safety check-ins" subtitle="Follow up with volunteers; location is only shown if they chose to submit it">
        <SafeTable headers={['TASK', 'VOLUNTEER', 'TIME', 'STATUS', 'ACTION']} empty="No overdue safety alerts.">
          {alerts.map(a => <tr key={a.id}><td>{shortId(a.taskId)}</td><td>{shortId(a.volunteerId)}</td><td>{new Date(a.at).toLocaleString()}</td><td><Pill>{a.status}</Pill></td>
            <td><Button variant="outline" onClick={() => {
              const notes = window.prompt('Record follow-up and resolution:');
              if (notes) act(() => patch(`/volunteers/check-ins/${a.id}/resolve`, { notes }), 'Safety follow-up recorded.');
            }}>Resolve</Button></td></tr>)}
        </SafeTable>
      </Panel>
      <Panel title="Active field safety monitor" subtitle="Check-in timing is event-based; volunteers are not continuously tracked">
        <SafeTable headers={['TASK', 'VOLUNTEER', 'LAST CHECK-IN', 'STATUS', 'VOLUNTARY AREA']} empty="No active field assignments.">
          {safetyStatus.map(x => <tr key={x.id}><td>{shortId(x.id)}</td><td>{shortId(x.volunteerId)}</td>
            <td>{x.lastCheckInAt ? new Date(x.lastCheckInAt).toLocaleString() : 'Not checked in yet'}</td>
            <td><Pill>{x.safetyStatus}</Pill></td><td>{x.voluntaryArea || 'Not shared'}</td></tr>)}
        </SafeTable>
      </Panel>
    </>}

    <Panel title={manage ? 'Assignment history' : 'My assignments'} subtitle="Assigned → Accepted → In Progress → Completed; volunteers may decline with a reason">
      {!manage && tasks.some(t => ['Accepted', 'In Progress'].includes(t.status)) && <Field label="Optional check-in area"><input id="check-in-area" placeholder="Share an area only if you choose" /></Field>}
      <Field label="Decline reason"><input value={declineReason} onChange={e => setDeclineReason(e.target.value)} placeholder="Required only when declining" /></Field>
      <div className="inline-form">
        <Field label="Completion hours"><input type="number" min="0" max="24" step="0.25" value={completion.hoursWorked} onChange={e => setCompletion(s => ({ ...s, hoursWorked: e.target.value }))} /></Field>
        <Field label="Completion notes"><input value={completion.notes} onChange={e => setCompletion(s => ({ ...s, notes: e.target.value }))} /></Field>
      </div>
      <SafeTable headers={['TASK', 'REQUIRED SKILL', 'DESCRIPTION', 'STATUS', 'ACTION']} empty="No volunteer assignments yet.">
        {tasks.map(t => <tr key={t.id}><td><strong>{shortId(t.id)}</strong></td><td>{t.requiredSkill}</td><td>{t.description}</td><td><Pill>{t.status}</Pill></td><td>
          {!manage && ['Assigned', 'Reassigned'].includes(t.status) && <>
            <Button variant="outline" onClick={() => act(() => patch(`/volunteers/tasks/${t.id}`, { status: 'Accepted' }), 'Task accepted.')}>Accept</Button>
            <Button variant="outline" disabled={!declineReason.trim()} onClick={() => act(() => patch(`/volunteers/tasks/${t.id}`, { status: 'Declined', reason: declineReason }), 'Task declined.')}>Decline</Button>
          </>}
          {!manage && t.status === 'Accepted' && <>
            <Button variant="outline" onClick={() => act(() => patch(`/volunteers/tasks/${t.id}`, { status: 'In Progress' }), 'Task started.')}>Start</Button>
            <Button variant="outline" onClick={() => act(async () => {
              await post(`/volunteers/tasks/${t.id}/check-ins`, { area: document.getElementById('check-in-area')?.value || '' });
            }, 'Safety check-in submitted.')}>Safety check-in</Button>
          </>}
          {!manage && t.status === 'In Progress' && <>
            <Button variant="outline" onClick={() => act(() => patch(`/volunteers/tasks/${t.id}`, {
              status: 'Completed', hoursWorked: completion.hoursWorked === '' ? null : Number(completion.hoursWorked), notes: completion.notes
            }), 'Task completion submitted for coordinator verification.')}>Complete</Button>
            <Button variant="outline" onClick={() => act(async () => {
              await post(`/volunteers/tasks/${t.id}/check-ins`, { area: document.getElementById('check-in-area')?.value || '' });
            }, 'Safety check-in submitted.')}>Safety check-in</Button>
          </>}
          {manage && !['Completed', 'Cancelled', 'Declined'].includes(t.status) && <Button variant="outline" onClick={() => act(() => patch(`/volunteers/tasks/${t.id}`, { status: 'Cancelled' }), 'Task cancelled.')}>Cancel</Button>}
          {manage && t.status === 'Completed' && !t.coordinatorVerified && <Button variant="outline" onClick={() => act(() => patch(`/volunteers/tasks/${t.id}`, { verifyCompletion: true }), 'Completion verified.')}>Verify completion</Button>}
          {manage && t.status === 'Completed' && t.coordinatorVerified && <Pill>Verified</Pill>}
          {!manage && !['Assigned', 'Accepted', 'In Progress'].includes(t.status) && '-'}
        </td></tr>)}
      </SafeTable>
    </Panel>
  </>;
}
