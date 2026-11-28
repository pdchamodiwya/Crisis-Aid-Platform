import { useEffect, useState } from 'react';
import { get } from '../../api';
import { downloadCsv } from '../../utils/downloadCsv';
import { DataTable } from './DonationUI';

export default function TransparencyReports() {
  const [campaigns, setCampaigns] = useState([]);
  const [report, setReport] = useState(null);
  const [personal, setPersonal] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    Promise.all([
      get('/campaigns'),
      get('/donations/report/me')
    ]).then(([c, p]) => {
      setCampaigns(c);
      setPersonal(p);
    }).catch(e => setError(e.message));
  }, []);

  async function loadReport(id) {
    if (!id) {
      setReport(null);
      return;
    }

    try {
      setReport(await get(`/campaigns/${id}/summary`));
    } catch (e) {
      setError(e.message);
    }
  }

  function exportReport() {
    downloadCsv('campaign-report.csv', [
      ['Campaign', 'Currency', 'Received', 'Allocated', 'Spent'],
      [
        report.name,
        report.currency,
        report.received,
        report.allocated,
        report.spent
      ]
    ]);
  }

  return (
    <section>
      <h2>Transparency Reports</h2>

      {error && <p>{error}</p>}

      <select onChange={e => loadReport(e.target.value)}>
        <option value="">Select campaign</option>

        {campaigns.map(c => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </select>

      {report && (
        <>
          <div className="member05-summary">
            {[
              ['Target', report.targetAmount],
              ['Received', report.received],
              ['Allocated', report.allocated],
              ['Spent', report.spent],
              ['Unallocated', report.unallocated],
              ['Unspent', report.unspent]
            ].map(([label, value]) => (
              <article key={label}>
                <p>{label}</p>
                <h3>
                  {report.currency} {
                    Number(value).toLocaleString()
                  }
                </h3>
              </article>
            ))}
          </div>

          <h3>Fundraising progress</h3>

          <progress
            value={Math.min(100, report.progress)}
            max="100"
          />

          <p>{report.progress.toFixed(1)}%</p>

          <DataTable
            columns={['Category', 'Amount spent']}
            rows={report.breakdown.map(b => [
              b.category,
              `${report.currency} ${b.amount}`
            ])}
          />

          <button onClick={exportReport}>
            Download Campaign Report
          </button>
        </>
      )}

      <h2>My Contributions</h2>

      <DataTable
        columns={['Campaign', 'Type', 'Amount', 'Status']}
        rows={personal.map(d => [
          d.campaignName,
          d.kind,
          `${d.currency} ${d.amount}`,
          d.status
        ])}
      />
    </section>
  );
}