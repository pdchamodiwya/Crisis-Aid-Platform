import { useEffect, useState } from 'react';
import { get, post } from '../../api';
import {
  DataForm, field, option, categories
} from './DonationUI';

export default function MakeDonation() {
  const [kind, setKind] = useState('Monetary');
  const [campaigns, setCampaigns] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    get('/campaigns')
      .then(data => setCampaigns(
        data.filter(c => c.status === 'Open')
      ))
      .catch(e => setError(e.message));
  }, []);

  async function submit(form) {
    const campaign = campaigns.find(
      c => c.id === form.campaignId
    );

    if (!campaign) {
      throw new Error('Select a valid campaign.');
    }

    if (kind === 'Monetary') {
      await post('/donations', {
        campaignId: campaign.id,
        amount: Number(form.amount),
        currency: campaign.currency,
        notes: form.notes || null
      });
    } else {
      await post('/donations/material', {
        campaignId: campaign.id,
        category: form.category,
        itemName: form.itemName,
        quantity: Number(form.quantity),
        unit: form.unit,
        condition: form.condition,
        receivingLocation: form.receivingLocation
      });
    }
  }

  const fields = [
    field('campaignId', 'Select campaign', {
      options: campaigns.map(c =>
        option(c.id, `${c.name} (${c.currency})`))
    })
  ];

  if (kind === 'Monetary') {
    fields.push(
      field('amount', 'Donation amount', {
        type: 'number',
        min: 0.01,
        step: 0.01
      }),
      field('notes', 'Notes', { optional: true })
    );
  } else {
    fields.push(
      field('category', 'Category', {
        options: categories
      }),
      field('itemName', 'Item name'),
      field('quantity', 'Quantity', {
        type: 'number', min: 1, step: 1
      }),
      field('unit', 'Unit', {
        options: ['Packs', 'Boxes', 'Kg', 'Litres', 'Units']
          .map(v => option(v))
      }),
      field('condition', 'Condition', {
        options: ['New', 'Good'].map(v => option(v))
      }),
      field('receivingLocation', 'Receiving location')
    );
  }

  return (
    <section>
      <h2>Make a Donation</h2>

      {error && <p>{error}</p>}

      <select
        value={kind}
        onChange={e => setKind(e.target.value)}
      >
        <option>Monetary</option>
        <option>Material</option>
      </select>

      <p>
        All donations require verification by an
        authorised officer.
      </p>

      <DataForm
        key={kind}
        fields={fields}
        onSubmit={submit}
        button="Submit Donation"
      />
    </section>
  );
}