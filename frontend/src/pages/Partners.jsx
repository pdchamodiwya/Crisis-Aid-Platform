import { useEffect, useState } from 'react';
import {
  Building2, PackagePlus, Check, X, ArrowRightLeft, Send,
  Inbox, Truck, CheckCircle, Clock, ShieldCheck, MapPin
} from 'lucide-react';
import { get, post, patch, can, formatDate, shortId } from '../api';
import { SectionHeading, Panel, Field, Button, ErrorMessage, Success, Pill, Empty, SafeTable } from '../components/UI';

export default function Partners({ user }) {
  const [tab, setTab] = useState('offers'); // offers, requests, transfers
  const [orgs, setOrgs] = useState([]);
  const [offers, setOffers] = useState([]);
  const [requests, setRequests] = useState([]);
  const [transfers, setTransfers] = useState([]);
  const [resources, setResources] = useState([]);

  const [error, setError] = useState('');
  const [ok, setOk] = useState('');
  const [busy, setBusy] = useState(false);

  // Forms
  const [offerForm, setOfferForm] = useState({
    organisationId: '', resourceCategory: 'Food', area: 'Colombo', quantity: 10, notes: ''
  });

  const [requestForm, setRequestForm] = useState({
    targetOrganisationId: '', category: 'Food', quantity: 20, urgency: 'High', notes: ''
  });

  const [transferForm, setTransferForm] = useState({
    toOrganisationId: '', category: 'Food', resourceName: 'Emergency Rice Sacks', quantity: 25, partnerOfferId: null
  });

  const [receiveModal, setReceiveModal] = useState(null);
  const [receiveForm, setReceiveForm] = useState({ confirmedQuantity: 1, discrepancyNotes: '', creditResourceId: '' });

  const canReview = can(user, 'EmergencyCoordinator', 'LogisticsManager', 'SystemAdministrator');
  const canOffer = canReview || can(user, 'PartnerOfficer');

  const loadData = async () => {
    try {
      const [oList, offList, reqList, trfList, resList] = await Promise.all([
        get('/partners'),
        get('/partners/offers'),
        get('/partners/requests').catch(() => []),
        get('/partners/transfers').catch(() => []),
        get('/resources').catch(() => [])
      ]);

      setOrgs(oList || []);
      setOffers(offList || []);
      setRequests(reqList || []);
      setTransfers(trfList || []);
      setResources(resList || []);

      if (oList?.length > 0) {
        setOfferForm(f => ({ ...f, organisationId: f.organisationId || oList[0].id }));
        setRequestForm(f => ({ ...f, targetOrganisationId: f.targetOrganisationId || oList[0].id }));
        setTransferForm(f => ({ ...f, toOrganisationId: f.toOrganisationId || oList[0].id }));
      }
    } catch (e) {
      setError(e.message);
    }
  };

  useEffect(() => {
    loadData();
  }, [tab]);

  const handleAction = async (action, msg) => {
    setError('');
    setOk('');
    setBusy(true);
    try {
      await action();
      setOk(msg);
      await loadData();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  // Submit Partner Offer
  const submitOffer = (e) => {
    e.preventDefault();
    handleAction(async () => {
      await post('/partners/offers', {
        ...offerForm,
        quantity: Number(offerForm.quantity)
      });
      setOfferForm(f => ({ ...f, notes: '', quantity: 10 }));
    }, 'Partner resource offer registered for coordination review.');
  };

  // Review Offer (Accept/Decline)
  const reviewOffer = (id, status) => {
    handleAction(async () => {
      await patch(`/partners/offers/${id}/status`, { status });
    }, `Partner offer marked as ${status}.`);
  };

  // Submit Request to Partner
  const submitPartnerRequest = (e) => {
    e.preventDefault();
    handleAction(async () => {
      await post('/partners/requests', {
        ...requestForm,
        quantity: Number(requestForm.quantity)
      });
      setRequestForm(f => ({ ...f, notes: '', quantity: 20 }));
    }, 'Resource requisition dispatched to partner organisation.');
  };

  // Submit Stock Transfer
  const submitTransfer = (e) => {
    e.preventDefault();
    handleAction(async () => {
      await post('/partners/transfers', {
        ...transferForm,
        quantity: Number(transferForm.quantity)
      });
      setTransferForm(f => ({ ...f, resourceName: '', quantity: 25 }));
    }, 'Inter-agency stock transfer initiated and placed in transit.');
  };

  // Update Transfer Status
  const handleTransferStatus = (id, status) => {
    handleAction(async () => {
      await patch(`/partners/transfers/${id}/status`, { status });
    }, `Transfer status updated to "${status}".`);
  };

  // Confirm Received Transfer
  const submitReceiveTransfer = (e) => {
    e.preventDefault();
    if (!receiveModal) return;
    handleAction(async () => {
      await post(`/partners/transfers/${receiveModal.id}/receive`, {
        confirmedQuantity: Number(receiveForm.confirmedQuantity),
        discrepancyNotes: receiveForm.discrepancyNotes,
        creditResourceId: receiveForm.creditResourceId || null
      });
      setReceiveModal(null);
    }, 'Stock transfer received and credited to physical warehouse inventory!');
  };

  const categories = ['Food', 'Water', 'Medicine', 'Medical equipment', 'Clothing', 'Shelter materials', 'Hygiene products', 'Transportation supplies', 'Emergency equipment', 'Other'];

  return (
    <>
      <SectionHeading
        kicker="MULTI-ORGANISATION / PARTNER COORDINATION & AID TRANSFERS"
        title="Humanitarian Partner Workspace"
        subtitle="Coordinate resource pledges, inter-agency aid requests and physical stock transfers between NGOs"
        action={<span className="soft-label"><Building2 size={15} /> {orgs.length} Verified Partner Organisations</span>}
      />

      <ErrorMessage error={error} />
      <Success message={ok} />

      {/* TABS */}
      <div className="tab-bar">
        {[
          { key: 'offers', label: 'Resource Offers & Pledges', icon: PackagePlus },
          { key: 'requests', label: 'Requests to Partners', icon: Send },
          { key: 'transfers', label: 'Inter-Agency Stock Transfers', icon: ArrowRightLeft }
        ].map(t => (
          <button
            key={t.key}
            className={`tab-btn ${tab === t.key ? 'active' : ''}`}
            onClick={() => { setTab(t.key); setError(''); setOk(''); }}
          >
            <t.icon size={16} />
            <span>{t.label}</span>
          </button>
        ))}
      </div>

      {/* =========================================================================
          TAB 1: PARTNER OFFERS
      ========================================================================= */}
      {tab === 'offers' && (
        <div className="dash-grid">
          <Panel
            title="Partner Resource Offers & Pledges"
            subtitle="Offers require emergency coordinator or logistics manager review"
          >
            {offers.length > 0 ? (
              offers.map(o => (
                <div className="ops-record" key={o.id}>
                  <div className="ops-icon"><PackagePlus size={20} /></div>
                  <div className="ops-data">
                    <strong>{o.quantity} × {o.resourceCategory}</strong>
                    <span>{o.area || 'General Area'} · {o.notes || 'No extra notes provided'}</span>
                    <small>Logged: {formatDate(o.createdAt)}</small>
                    {canReview && o.status === 'Offered' && (
                      <div className="record-actions">
                        <Button onClick={() => reviewOffer(o.id, 'Accepted')}>
                          <Check size={14} /> Accept Offer
                        </Button>
                        <Button variant="secondary" onClick={() => reviewOffer(o.id, 'Declined')}>
                          <X size={14} /> Decline
                        </Button>
                      </div>
                    )}
                  </div>
                  <Pill tone={o.status === 'Accepted' ? 'green' : o.status === 'Declined' ? 'red' : 'amber'}>
                    {o.status}
                  </Pill>
                </div>
              ))
            ) : (
              <Empty text="No partner offers recorded yet." />
            )}
          </Panel>

          <Panel
            title="Register a Resource Offer"
            subtitle="Pledge aid resources from partner humanitarian organisations"
          >
            {canOffer ? (
              <form className="form-grid" onSubmit={submitOffer}>
                <Field label="Partner Organisation">
                  <select
                    required
                    value={offerForm.organisationId}
                    onChange={e => setOfferForm({ ...offerForm, organisationId: e.target.value })}
                  >
                    {orgs.map(x => <option key={x.id} value={x.id}>{x.name} ({x.type})</option>)}
                  </select>
                </Field>

                <Field label="Aid Category">
                  <select
                    value={offerForm.resourceCategory}
                    onChange={e => setOfferForm({ ...offerForm, resourceCategory: e.target.value })}
                  >
                    {categories.map(x => <option key={x} value={x}>{x}</option>)}
                  </select>
                </Field>

                <Field label="Supply Depot Area">
                  <input
                    value={offerForm.area}
                    maxLength={120}
                    onChange={e => setOfferForm({ ...offerForm, area: e.target.value })}
                  />
                </Field>

                <Field label="Offered Units">
                  <input
                    type="number"
                    min="1"
                    required
                    value={offerForm.quantity}
                    onChange={e => setOfferForm({ ...offerForm, quantity: e.target.value })}
                  />
                </Field>

                <Field label="Offer Notes / Transport Readiness">
                  <textarea
                    rows="3"
                    maxLength={1200}
                    value={offerForm.notes}
                    placeholder="Batch details, packing info, delivery availability..."
                    onChange={e => setOfferForm({ ...offerForm, notes: e.target.value })}
                  />
                </Field>

                <Button type="submit" disabled={busy || !offerForm.organisationId}>
                  <PackagePlus size={16} /> {busy ? 'Saving...' : 'Register Offer'}
                </Button>
              </form>
            ) : (
              <Empty text="You do not have permission to register partner offers." />
            )}
          </Panel>
        </div>
      )}

      {/* =========================================================================
          TAB 2: REQUESTS TO PARTNERS
      ========================================================================= */}
      {tab === 'requests' && (
        <div className="dash-grid">
          <Panel
            title="Inter-Agency Resource Requisitions"
            subtitle="Active aid requisitions sent to partnering NGOs and agencies"
          >
            <SafeTable
              headers={['CATEGORY', 'QUANTITY', 'TARGET NGO', 'URGENCY', 'STATUS', 'ACTIONS']}
              empty="No outgoing partner requests registered."
            >
              {requests.map(r => {
                const targetOrg = orgs.find(o => o.id === r.targetOrganisationId);
                return (
                  <tr key={r.id}>
                    <td><strong>{r.category}</strong></td>
                    <td>{r.quantity} units</td>
                    <td>{targetOrg?.name || 'Partner NGO'}</td>
                    <td><Pill>{r.urgency}</Pill></td>
                    <td>
                      <Pill tone={r.status === 'Fulfilled' ? 'green' : r.status === 'Declined' ? 'red' : 'blue'}>
                        {r.status}
                      </Pill>
                    </td>
                    <td>
                      {canReview && r.status === 'Requested' && (
                        <div className="btn-group-sm">
                          <button className="btn-link" onClick={() => handleAction(() => patch(`/partners/requests/${r.id}/status`, { status: 'Acknowledged' }), 'Request acknowledged')}>
                            Acknowledge
                          </button>
                          <button className="btn-link" onClick={() => handleAction(() => patch(`/partners/requests/${r.id}/status`, { status: 'Fulfilled' }), 'Request marked fulfilled')}>
                            Mark Fulfilled
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })}
            </SafeTable>
          </Panel>

          <Panel
            title="Requisition Supplies from Partner"
            subtitle="Request emergency supplies from partner organisations during critical shortages"
          >
            {canReview ? (
              <form className="form-grid" onSubmit={submitPartnerRequest}>
                <Field label="Target Partner Organisation">
                  <select
                    required
                    value={requestForm.targetOrganisationId}
                    onChange={e => setRequestForm({ ...requestForm, targetOrganisationId: e.target.value })}
                  >
                    {orgs.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
                  </select>
                </Field>

                <Field label="Category Needed">
                  <select
                    value={requestForm.category}
                    onChange={e => setRequestForm({ ...requestForm, category: e.target.value })}
                  >
                    {categories.map(x => <option key={x} value={x}>{x}</option>)}
                  </select>
                </Field>

                <Field label="Required Units">
                  <input
                    type="number"
                    min="1"
                    required
                    value={requestForm.quantity}
                    onChange={e => setRequestForm({ ...requestForm, quantity: e.target.value })}
                  />
                </Field>

                <Field label="Urgency Level">
                  <select
                    value={requestForm.urgency}
                    onChange={e => setRequestForm({ ...requestForm, urgency: e.target.value })}
                  >
                    <option value="Critical">Critical (Immediate Need)</option>
                    <option value="High">High</option>
                    <option value="Medium">Medium</option>
                    <option value="Low">Low</option>
                  </select>
                </Field>

                <Field label="Justification & Need Description">
                  <textarea
                    rows="3"
                    value={requestForm.notes}
                    placeholder="Shortage justification, target population, delivery address..."
                    onChange={e => setRequestForm({ ...requestForm, notes: e.target.value })}
                  />
                </Field>

                <Button type="submit" disabled={busy || !requestForm.targetOrganisationId}>
                  <Send size={16} /> Send Requisition
                </Button>
              </form>
            ) : (
              <Empty text="You do not have permission to issue partner requisitions." />
            )}
          </Panel>
        </div>
      )}

      {/* =========================================================================
          TAB 3: STOCK TRANSFERS
      ========================================================================= */}
      {tab === 'transfers' && (
        <div className="dash-grid">
          <Panel
            title="Inter-Agency Stock Transfers & Shipments"
            subtitle="Pipeline: Requested → Offered → Accepted → In Transit → Received (Credited to Inventory)"
          >
            <SafeTable
              headers={['TRANSFER', 'FROM → TO', 'QUANTITY', 'STATUS', 'DATES', 'ACTIONS']}
              empty="No inter-agency stock transfers recorded."
            >
              {transfers.map(t => (
                <tr key={t.id}>
                  <td>
                    <strong>{t.resourceName}</strong>
                    <div className="subtext">{t.category}</div>
                  </td>
                  <td>
                    <div>{t.fromOrganisationName}</div>
                    <div className="subtext">➔ {t.toOrganisationName}</div>
                  </td>
                  <td><strong>{t.quantity}</strong> units</td>
                  <td>
                    <Pill tone={t.status === 'Received' ? 'green' : t.status === 'InTransit' ? 'blue' : 'amber'}>
                      {t.status}
                    </Pill>
                  </td>
                  <td>
                    <div>Sent: {formatDate(t.sentAt)}</div>
                    {t.receivedAt && <div className="green-text">Received: {formatDate(t.receivedAt)}</div>}
                  </td>
                  <td>
                    <div className="btn-group-sm">
                      {canReview && t.status === 'Accepted' && (
                        <button className="btn-link" onClick={() => handleTransferStatus(t.id, 'InTransit')}>
                          Mark In Transit
                        </button>
                      )}
                      {canReview && t.status === 'InTransit' && (
                        <button
                          className="btn-link"
                          onClick={() => {
                            setReceiveModal(t);
                            setReceiveForm({ confirmedQuantity: t.quantity, discrepancyNotes: '', creditResourceId: '' });
                          }}
                        >
                          <CheckCircle size={14} /> Confirm Receipt
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </SafeTable>
          </Panel>

          <Panel
            title="Initiate Inter-Agency Transfer"
            subtitle="Dispatch inventory to partnering field organisation"
          >
            {canReview ? (
              <form className="form-grid" onSubmit={submitTransfer}>
                <Field label="Destination Partner Organisation">
                  <select
                    required
                    value={transferForm.toOrganisationId}
                    onChange={e => setTransferForm({ ...transferForm, toOrganisationId: e.target.value })}
                  >
                    {orgs.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
                  </select>
                </Field>

                <Field label="Resource Item Name">
                  <input
                    required
                    value={transferForm.resourceName}
                    placeholder="e.g. Purified Drinking Water Packs"
                    onChange={e => setTransferForm({ ...transferForm, resourceName: e.target.value })}
                  />
                </Field>

                <Field label="Category">
                  <select
                    value={transferForm.category}
                    onChange={e => setTransferForm({ ...transferForm, category: e.target.value })}
                  >
                    {categories.map(x => <option key={x} value={x}>{x}</option>)}
                  </select>
                </Field>

                <Field label="Quantity to Transfer">
                  <input
                    type="number"
                    min="1"
                    required
                    value={transferForm.quantity}
                    onChange={e => setTransferForm({ ...transferForm, quantity: e.target.value })}
                  />
                </Field>

                <Button type="submit" disabled={busy || !transferForm.toOrganisationId}>
                  <ArrowRightLeft size={16} /> Initiate Transfer Shipment
                </Button>
              </form>
            ) : (
              <Empty text="Only logistics managers and coordinators can initiate stock transfers." />
            )}
          </Panel>
        </div>
      )}

      {/* CONFIRM RECEIPT MODAL */}
      {receiveModal && (
        <div className="modal-backdrop">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Confirm Received Transfer: {receiveModal.resourceName}</h3>
              <button className="icon-btn" onClick={() => setReceiveModal(null)}><X size={18} /></button>
            </div>
            <form onSubmit={submitReceiveTransfer} className="form-grid">
              <div className="subtext">
                Shipped: {receiveModal.quantity} units from {receiveModal.fromOrganisationName}
              </div>

              <Field label="Confirmed Received Quantity">
                <input
                  type="number"
                  min="1"
                  max={receiveModal.quantity * 2}
                  required
                  value={receiveForm.confirmedQuantity}
                  onChange={e => setReceiveForm({ ...receiveForm, confirmedQuantity: e.target.value })}
                />
              </Field>

              <Field label="Credit to Existing Inventory Batch (Optional)">
                <select
                  value={receiveForm.creditResourceId}
                  onChange={e => setReceiveForm({ ...receiveForm, creditResourceId: e.target.value })}
                >
                  <option value="">-- Create new inventory batch --</option>
                  {resources.filter(r => r.category === receiveModal.category).map(r => (
                    <option key={r.id} value={r.id}>{r.name} ({r.area}) · Current: {r.availableQuantity} {r.unit}</option>
                  ))}
                </select>
              </Field>

              <Field label="Discrepancy / Inspection Notes">
                <textarea
                  rows="2"
                  value={receiveForm.discrepancyNotes}
                  placeholder="Record any packaging damage, seal tampering, or quantity differences..."
                  onChange={e => setReceiveForm({ ...receiveForm, discrepancyNotes: e.target.value })}
                />
              </Field>

              <div className="modal-footer">
                <Button type="submit"><Check size={16} /> Credit to Physical Inventory</Button>
                <Button variant="secondary" onClick={() => setReceiveModal(null)}>Cancel</Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
