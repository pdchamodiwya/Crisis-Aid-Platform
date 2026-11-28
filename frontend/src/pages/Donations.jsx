import { useState } from 'react';
import LegacyDonations from './LegacyDonations';
import './donations/donations.css';

export default function Donations({ user }) {
  const [activeTab, setActiveTab] = useState('campaigns');

  const isFundingOfficer =
    ['FundingOfficer', 'SystemAdministrator']
      .includes(user?.role);

  const isDonor =
    ['Donor', 'Citizen'].includes(user?.role);

  const tabs = [
    { id: 'campaigns', label: 'Campaigns' }
  ];

  return (
    <section>
      <h1>Donation Management</h1>

      <div className="member05-tabs">
        {tabs.map(tab => (
          <button
            key={tab.id}
            className={
              activeTab === tab.id
                ? 'btn primary'
                : 'btn outline'
            }
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'campaigns' &&
        <LegacyDonations user={user} />}
    </section>
  );
}