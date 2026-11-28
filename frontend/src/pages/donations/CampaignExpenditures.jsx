import { useEffect, useState } from 'react';
import { get, post } from '../../api';
import {
  DataForm, DataTable, field, option
} from './DonationUI';

export default function CampaignExpenditures() {
  const [allocations, setAllocations] = useState([]);
  const [expenses, setExpenses] = useState([]);
  const [error, setError] = useState('');

  async function load() {
    const [a, e] = await Promise.all([
      get('/donation-allocations'),
      get('/expenditures')
    ]);

    setAllocations(a);
    setExpenses(e);
  }

  useEffect(() => {
    load().catch(e => setError(e.message));
  }, []);

  async function record(form) {
    await post('/expenditures', {
      allocationId: form.allocationId,
      description: form.description,
      amount: Number(form.amount),
      reference: form.reference
    });

    await load();
  }

  return (
    <section>
      <h2>Campaign Expenditures</h2>

      {error && <p>{error}</p>}

      <DataForm
        button="Record Expenditure"
        onSubmit={record}
        fields={[
          field('allocationId', 'Select allocation', {
            options: allocations
              .filter(a =>
                a.status === 'Approved' &&
                a.amount > a.spent
              )
              .map(a => option(
                a.id,
                `${a.category}: ${a.currency} ${
                  (a.amount - a.spent).toFixed(2)
                } remaining`
              ))
          }),
          field('description', 'Expenditure description'),
          field('reference', 'Invoice reference'),
          field('amount', 'Amount spent', {
            type: 'number', min: 0.01, step: 0.01
          })
        ]}
      />

      <DataTable
        columns={[
          'Campaign', 'Category', 'Description',
          'Reference', 'Amount', 'Date'
        ]}
        rows={expenses.map(e => [
          e.campaignName,
          e.category,
          e.description,
          e.reference,
          `${e.currency} ${e.amount}`,
          new Date(e.spentAt).toLocaleDateString()
        ])}
      />
    </section>
  );
}