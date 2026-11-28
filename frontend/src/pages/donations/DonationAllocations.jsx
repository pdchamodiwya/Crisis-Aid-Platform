import { useEffect, useState } from 'react';
import { get, post } from '../../api';
import {
  DataForm, DataTable, field, option, categories
} from './DonationUI';

export default function DonationAllocations() {
  const [donations, setDonations] = useState([]);
  const [allocations, setAllocations] = useState([]);
  const [error, setError] = useState('');

  async function load() {
    const [d, a] = await Promise.all([
      get('/donations'),
      get('/donation-allocations')
    ]);

    setDonations(d.filter(x =>
      x.status === 'Confirmed' &&
      x.kind === 'Monetary'
    ));

    setAllocations(a);
  }

  useEffect(() => {
    load().catch(e => setError(e.message));
  }, []);

  function available(donation) {
    const total = allocations
      .filter(a =>
        a.donationId === donation.id &&
        a.status === 'Approved'
      )
      .reduce((sum, a) => sum + Number(a.amount), 0);

    return Number(donation.amount) - total;
  }

  async function allocate(form) {
    await post('/donation-allocations', {
      donationId: form.donationId,
      category: form.category,
      purpose: form.purpose,
      amount: Number(form.amount)
    });

    await load();
  }

  return (
    <section>
      <h2>Fund Allocation</h2>
      {error && <p>{error}</p>}

      <DataForm
        fields={[
          field('donationId', 'Confirmed donation', {
            options: donations
              .filter(d => available(d) > 0)
              .map(d => option(
                d.id,
                `${d.campaignName} - ${d.currency} ${
                  available(d).toFixed(2)
                } available`
              ))
          }),
          field('category', 'Resource category', {
            options: categories
          }),
          field('purpose', 'Allocation purpose'),
          field('amount', 'Allocation amount', {
            type: 'number', min: 0.01, step: 0.01
          })
        ]}
        button="Allocate Funds"
        onSubmit={allocate}
      />

      <h2>Allocation History</h2>

      <DataTable
        columns={[
          'Campaign', 'Category',
          'Purpose', 'Amount', 'Status'
        ]}
        rows={allocations.map(a => [
          a.campaignName,
          a.category,
          a.purpose,
          `${a.currency} ${a.amount}`,
          a.status
        ])}
      />
    </section>
  );
}