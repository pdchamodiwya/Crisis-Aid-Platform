import { useEffect, useState } from 'react';
import { get, patch } from '../../api';
import {
  DataForm, DataTable, field, option
} from './DonationUI';

export default function FraudInvestigation() {
  const [flags, setFlags] = useState([]);
  const [error, setError] = useState('');

  async function load() {
    setFlags(await get('/donation-flags'));
  }

  useEffect(() => {
    load().catch(e => setError(e.message));
  }, []);

  async function review(form) {
    await patch(`/donation-flags/${form.id}`, {
      status: form.status,
      investigationNotes: form.notes
    });

    await load();
  }

  return (
    <section>
      <h2>Suspicious Donation Investigations</h2>

      {error && <p>{error}</p>}

      <div className="member05-summary">
        {['Open', 'Investigating', 'Resolved'].map(s => (
          <article key={s}>
            <p>{s}</p>
            <h3>{flags.filter(f => f.status === s).length}</h3>
          </article>
        ))}
      </div>

      <DataTable
        columns={['Campaign', 'Reason', 'Severity', 'Status']}
        rows={flags.map(f => [
          f.campaignName,
          f.reason,
          f.severity,
          f.status
        ])}
      />

      <DataForm
        button="Save Investigation"
        onSubmit={review}
        fields={[
          field('id', 'Select investigation', {
            options: flags.map(f => option(f.id, f.reason))
          }),
          field('status', 'Investigation status', {
            options: [
              'Investigating', 'Resolved', 'FalsePositive'
            ].map(v => option(v))
          }),
          field('notes', 'Investigation notes')
        ]}
      />
    </section>
  );
}