import CampaignManagement from './donations/CampaignManagement';
import { useState } from 'react';
import LegacyDonations from './LegacyDonations';
import './donations/donations.css';
import MakeDonation from './donations/MakeDonation';
import DonationRegister from './donations/DonationRegister';
import DonationAllocations from './donations/DonationAllocations';
import CampaignExpenditures from './donations/CampaignExpenditures';
import TransparencyReports from './donations/TransparencyReports';

export default function Donations({ user }) {
  const [activeTab, setActiveTab] = useState('campaigns');

  const isFundingOfficer =
    ['FundingOfficer', 'SystemAdministrator']
      .includes(user?.role);

        (isFundingOfficer ? [
  { id: 'manage', label: 'Manage Campaigns' }
] : [])

(isFundingOfficer ? [
  { id: 'allocations', label: 'Fund Allocations' }
] : [])

(isFundingOfficer ? [
  { id: 'expenses', label: 'Expenditures' }
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
  [{ id: 'reports', label: 'Transparency Reports' }]


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

  {activeTab === 'allocations' && isFundingOfficer &&
  <DonationAllocations />}

  {activeTab === 'expenses' && isFundingOfficer &&
  <CampaignExpenditures />}
  {activeTab === 'reports' &&
  <TransparencyReports />}
    </section>
  );
  
  
}