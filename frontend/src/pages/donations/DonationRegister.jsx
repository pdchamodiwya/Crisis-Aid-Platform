import { useEffect, useState } from 'react';
import { get, post } from '../../api';
import {
  DataForm, DataTable, field, option
} from './DonationUI';

export default function DonationRegister({ user }) {
  const [donations, setDonations] = useState([]);
  const [selected, setSelected] = useState(null);
  const [error, setError] = useState('');

  const staff = [
    'FundingOfficer',
    'SystemAdministrator'
  ].includes(user?.role);

  async function load() {
    setDonations(await get('/donations'));
  }

  useEffect(() => {
    load().catch(e => setError(e.message));
  }, []);

  async function confirm(form) {
    await post(`/donations/${selected.id}/confirm`, {
      transactionReference: form.reference || null,
      paymentMethod: form.method || null,
      receivedQuantity: form.quantity
        ? Number(form.quantity)
        : null,
      notes: form.notes || null
    });

    await load();
    setSelected(null);
  }

  async function reject(id) {
    if (!window.confirm('Reject this donation?')) return;

    try {
      await post(`/donations/${id}/reject`, {});
      await load();
    } catch (e) {
      setError(e.message);
    }
  }

  return (
    <section>
      <h2>Donation Register</h2>

      {error && <p role="alert">{error}</p>}

      <DataTable
        columns={[
          'Campaign', 'Type', 'Amount',
          'Status', 'Date', 'Actions'
        ]}
        rows={donations.map(d => [
          d.campaignName,
          d.kind,
          d.kind === 'Monetary'
            ? `${d.currency} ${d.amount}`
            : 'Material items',
          d.status,
          new Date(d.at).toLocaleString(),
          staff &&
          ['PledgeRecorded', 'UnderReview'].includes(d.status)
            ? <div>
                <button onClick={() => setSelected(d)}>
                  Review
                </button>
                <button onClick={() => reject(d.id)}>
                  Reject
                </button>
              </div>
            : '-'
        ])}
      />

      {selected && staff && (
        <section>
          <h3>Review Donation</h3>

          <p>
            Verify the actual payment or received goods
            before confirming this donation.
          </p>

          <DataForm
            key={selected.id}
            button="Confirm Donation"
            onSubmit={confirm}
            fields={
              selected.kind === 'Monetary'
                ? [
                    field('method', 'Payment method', {
                      options: [
                        'Bank Transfer', 'Cash', 'Other'
                      ].map(v => option(v))
                    }),
                    field('reference', 'Transaction reference'),
                    field('notes', 'Verification notes')
                  ]
                : [
                    field('quantity', 'Received quantity', {
                      type: 'number', min: 1, step: 1
                    }),
                    field('notes', 'Verification notes')
                  ]
            }
          />
        </section>
      )}
    </section>
  );
}