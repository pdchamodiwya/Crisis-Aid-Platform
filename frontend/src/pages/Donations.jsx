import CampaignManagement from './donations/CampaignManagement';
import { useState } from 'react';
import LegacyDonations from './LegacyDonations';
import './donations/donations.css';
import MakeDonation from './donations/MakeDonation';
import DonationRegister from './donations/DonationRegister';

export default function Donations({ user }) {
  const [activeTab, setActiveTab] = useState('campaigns');

  const isFundingOfficer =
    ['FundingOfficer', 'SystemAdministrator']
      .includes(user?.role);

        (isFundingOfficer ? [
  { id: 'manage', label: 'Manage Campaigns' }
] : [])

  const isDonor =
    ['Donor', 'Citizen'].includes(user?.role);
    (isDonor ? [
  { id: 'donate', label: 'Make a Donation' }
] : [])

  const tabs = [
    { id: 'campaigns', label: 'Campaigns' }
  ];
  [{ id: 'history', label: 'Donation History' }]


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

        {activeTab === 'donate' && isDonor &&
  <MakeDonation />}

  {activeTab === 'history' &&
  <DonationRegister user={user} />}
    </section>
  );
  
  
}