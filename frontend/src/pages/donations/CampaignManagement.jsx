import { useEffect, useState } from 'react';
import { get, post, put, patch } from '../../api';
import {
  DataForm, DataTable, field, option
} from './DonationUI';

export default function CampaignManagement() {
  const [campaigns, setCampaigns] = useState([]);
  const [crises, setCrises] = useState([]);
  const [editing, setEditing] = useState('');
  const [error, setError] = useState('');

  async function load() {
    const [c, r] = await Promise.all([
      get('/campaigns'),
      get('/crises')
    ]);

    setCampaigns(c);
    setCrises(r);
  }

  useEffect(() => {
    load().catch(e => setError(e.message));
  }, []);

  async function save(form) {
    const payload = {
      name: form.name,
      description: form.description || '',
      crisisId: form.crisisId,
      targetAmount: Number(form.targetAmount),
      currency: form.currency,
      startDate: new Date(form.startDate).toISOString(),
      endDate: form.endDate
        ? new Date(form.endDate).toISOString()
        : null
    };

    if (editing) {
      await put(`/campaigns/${editing}`, payload);
    } else {
      await post('/campaigns', payload);
    }

    await load();
    setEditing('');
  }

  async function close(id) {
    if (!window.confirm('Close this campaign?')) return;

    try {
      await patch(`/campaigns/${id}/close`, {});
      await load();
    } catch (e) {
      setError(e.message);
    }
  }

  return (
    <section>
      <h2>{editing ? 'Edit Campaign' : 'Create Campaign'}</h2>

      {error && <p role="alert">{error}</p>}

      {editing && (
        <button onClick={() => setEditing('')}>
          Cancel editing
        </button>
      )}

      <DataForm
        key={editing}
        button={editing ? 'Update Campaign' : 'Create Campaign'}
        onSubmit={save}
        fields={[
          field('name', 'Campaign name'),
          field('description', 'Description', {
            optional: true
          }),
          field('crisisId', 'Related crisis', {
            options: crises.map(c =>
              option(c.id, c.title || c.name || c.id))
          }),
          field('targetAmount', 'Target amount', {
            type: 'number', min: 0.01, step: 0.01
          }),
          field('currency', 'Currency', {
            options: ['LKR', 'USD', 'EUR'].map(v => option(v))
          }),
          field('startDate', 'Start date', {
            type: 'date'
          }),
          field('endDate', 'End date', {
            type: 'date', optional: true
          })
        ]}
      />

      <h2>Existing Campaigns</h2>

      <DataTable
        columns={['Campaign', 'Target', 'Status', 'Actions']}
        rows={campaigns.map(c => [
          c.name,
          `${c.currency} ${c.targetAmount}`,
          c.status,
          <div>
            <button onClick={() => setEditing(c.id)}>
              Edit
            </button>
            <button onClick={() => close(c.id)}>
              Close
            </button>
          </div>
        ])}
      />
    </section>
  );
}