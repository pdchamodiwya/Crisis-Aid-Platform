import { useEffect, useState } from 'react';
import {
  PackagePlus, Truck, Filter, Check, Layers, AlertTriangle, ArrowUpDown, History,
  QrCode, RefreshCw, X, ShieldAlert, Boxes, MapPin, Calendar, Clock,
  CheckCircle, FileText, Send, RotateCcw, Search, Eye
} from 'lucide-react';
import { get, post, put, del, patch, shortId, formatDate, can } from '../api';
import { SectionHeading, Panel, Field, Button, Pill, Progress, ErrorMessage, Success, Empty, SafeTable } from '../components/UI';

export default function Resources({ user }) {
  const [tab, setTab] = useState('inventory'); // inventory, movements, allocations, dispatches, shortages, verification
  const [resources, setResources] = useState([]);
  const [requests, setRequests] = useState([]);
  const [matches, setMatches] = useState([]);
  const [dispatches, setDispatches] = useState([]);
  const [movements, setMovements] = useState([]);
  const [metrics, setMetrics] = useState(null);
  const [shortageData, setShortageData] = useState(null);
  const [shortagePlan, setShortagePlan] = useState(null);

  // Filters & Search
  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('All');
  const [areaFilter, setAreaFilter] = useState('All');
  const [lowStockFilter, setLowStockFilter] = useState(false);
  const [expiringFilter, setExpiringFilter] = useState(false);

  // Active Selections
  const [chosenReq, setChosenReq] = useState('');
  const [chosenRes, setChosenRes] = useState('');
  const [quantity, setQuantity] = useState(1);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false);

  // Modals & Popups
  const [showAddModal, setShowAddModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(null);
  const [showReplenishModal, setShowReplenishModal] = useState(null);
  const [showAdjustModal, setShowAdjustModal] = useState(null);
  const [showDispatchModal, setShowDispatchModal] = useState(null);
  const [showQrModal, setShowQrModal] = useState(null);

  // Forms
  const [form, setForm] = useState({
    name: '', category: 'Food', unit: 'packs', availableQuantity: 20, area: 'Colombo',
    latitude: 6.9271, longitude: 79.8612, accessible: true, description: '', condition: 'Good',
    batchNumber: '', expiresAt: ''
  });

  const [replenishForm, setReplenishForm] = useState({ quantity: 10, notes: 'Routine replenishment', batchNumber: '', expiresAt: '' });
  const [adjustForm, setAdjustForm] = useState({ newQuantity: 0, movementType: 'Stock Adjusted', reason: 'Physical inventory audit adjustment' });
  const [dispatchForm, setDispatchForm] = useState({
    allocationId: '', vehicleId: 'WP-CAD-1042', assignedDriver: 'Sunil Perera',
    departureLocation: 'Colombo Central Depot', destination: 'Relief Distribution Point A',
    dispatchedQuantity: 1, notes: 'Immediate relief dispatch'
  });

  // Verification Portal form
  const [verifyTokenInput, setVerifyTokenInput] = useState('');
  const [verifyPreview, setVerifyPreview] = useState(null);
  const [verifyDeliveredQty, setVerifyDeliveredQty] = useState(1);
  const [verifyNotes, setVerifyNotes] = useState('');

  const canManage = can(user, 'LogisticsManager', 'EmergencyCoordinator', 'SystemAdministrator');
  const isVolunteerOrDriver = can(user, 'Volunteer', 'LogisticsManager', 'EmergencyCoordinator', 'SystemAdministrator');

  const loadData = async () => {
    try {
      const [resList, reqList, mtr, dispList] = await Promise.all([
        get('/resources'),
        get('/requests'),
        get('/logistics/dashboard').catch(() => null),
        get('/dispatches').catch(() => [])
      ]);

      setResources(resList || []);
      setRequests((reqList || []).filter(x => ['Verified', 'Assigned'].includes(x.status)));
      setMetrics(mtr);
      setDispatches(dispList || []);

      if (tab === 'movements') {
        const moves = await get('/resources/movements').catch(() => []);
        setMovements(moves || []);
      } else if (tab === 'shortages') {
        const sht = await get('/resources/shortages/analysis').catch(() => null);
        setShortageData(sht);
      }
    } catch (e) {
      setError(e.message);
    }
  };

  useEffect(() => {
    loadData();
  }, [tab]);

  const handleAction = async (action, successMsg) => {
    setError('');
    setSuccess('');
    setBusy(true);
    try {
      await action();
      setSuccess(successMsg);
      await loadData();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const handleSelectRequest = async (id) => {
    setChosenReq(id);
    setChosenRes('');
    setMatches([]);
    if (!id) return;
    try {
      const resMatches = await get(`/requests/${id}/resource-matches`);
      setMatches(resMatches || []);
      const sel = requests.find(r => r.id === id);
      if (sel) setQuantity(sel.requestedQuantity);
    } catch (e) {
      setError(e.message);
    }
  };

  // Add Resource
  const submitAddResource = (e) => {
    e.preventDefault();
    handleAction(async () => {
      await post('/resources/create', {
        ...form,
        availableQuantity: Number(form.availableQuantity),
        latitude: Number(form.latitude),
        longitude: Number(form.longitude),
        expiresAt: form.expiresAt ? new Date(form.expiresAt).toISOString() : null
      });
      setShowAddModal(false);
      setForm({ name: '', category: 'Food', unit: 'packs', availableQuantity: 20, area: 'Colombo', latitude: 6.9271, longitude: 79.8612, accessible: true, description: '', condition: 'Good', batchNumber: '', expiresAt: '' });
    }, 'Resource added successfully and logged in stock movements.');
  };

  // Edit Resource
  const submitEditResource = (e) => {
    e.preventDefault();
    if (!showEditModal) return;
    handleAction(async () => {
      await put(`/resources/${showEditModal.id}`, {
        name: showEditModal.name,
        category: showEditModal.category,
        unit: showEditModal.unit,
        area: showEditModal.area,
        latitude: Number(showEditModal.latitude),
        longitude: Number(showEditModal.longitude),
        accessible: showEditModal.accessible,
        description: showEditModal.description || '',
        condition: showEditModal.condition || 'Good',
        batchNumber: showEditModal.batchNumber || '',
        isActive: showEditModal.isActive,
        expiresAt: showEditModal.expiresAt ? new Date(showEditModal.expiresAt).toISOString() : null
      });
      setShowEditModal(null);
    }, 'Resource details updated successfully.');
  };

  // Replenish Stock
  const submitReplenish = (e) => {
    e.preventDefault();
    if (!showReplenishModal) return;
    handleAction(async () => {
      await post(`/resources/${showReplenishModal.id}/replenish`, {
        quantity: Number(replenishForm.quantity),
        notes: replenishForm.notes,
        batchNumber: replenishForm.batchNumber,
        expiresAt: replenishForm.expiresAt ? new Date(replenishForm.expiresAt).toISOString() : null
      });
      setShowReplenishModal(null);
    }, `Stock replenished for ${showReplenishModal.name}. Stock Received movement recorded.`);
  };

  // Adjust Stock
  const submitAdjust = (e) => {
    e.preventDefault();
    if (!showAdjustModal) return;
    handleAction(async () => {
      await post(`/resources/${showAdjustModal.id}/adjust`, {
        newQuantity: Number(adjustForm.newQuantity),
        movementType: adjustForm.movementType,
        reason: adjustForm.reason
      });
      setShowAdjustModal(null);
    }, `Stock adjusted for ${showAdjustModal.name}. Movement logged.`);
  };

  // Deactivate
  const handleDeactivate = (res) => {
    if (!window.confirm(`Are you sure you want to deactivate "${res.name}"?`)) return;
    handleAction(async () => {
      await del(`/resources/${res.id}`);
    }, `Resource "${res.name}" deactivated.`);
  };

  // Allocate
  const submitAllocate = () => {
    if (!chosenReq || !chosenRes) return;
    handleAction(async () => {
      const res = await post('/resources/allocate', {
        requestId: chosenReq,
        resourceId: chosenRes,
        quantity: Number(quantity)
      });
      setChosenReq('');
      setChosenRes('');
      setMatches([]);
    }, 'Reservation confirmed with serializable transaction and queued notification.');
  };

  // Dispatch Allocation
  const submitDispatch = (e) => {
    e.preventDefault();
    if (!showDispatchModal) return;
    handleAction(async () => {
      await post(`/allocations/${showDispatchModal.allocationId}/dispatch`, {
        vehicleId: dispatchForm.vehicleId,
        assignedDriver: dispatchForm.assignedDriver,
        departureLocation: dispatchForm.departureLocation,
        destination: dispatchForm.destination,
        dispatchedQuantity: Number(dispatchForm.dispatchedQuantity),
        notes: dispatchForm.notes
      });
      setShowDispatchModal(null);
      setTab('dispatches');
    }, 'Aid allocation dispatched! Vehicle in transit and QR verification token generated.');
  };

  // Update Dispatch Status
  const handleDispatchStatus = (dispatchId, status) => {
    handleAction(async () => {
      await patch(`/dispatches/${dispatchId}/status`, { status });
    }, `Dispatch status updated to "${status}".`);
  };

  // Load QR Details
  const openQrModal = async (dispatchId) => {
    try {
      const qrData = await get(`/dispatches/${dispatchId}/qr`);
      setShowQrModal(qrData);
    } catch (e) {
      setError(e.message);
    }
  };

  // Shortage Plan Generation
  const generatePlan = async (category) => {
    setError('');
    try {
      const plan = await post('/resources/shortages/plan', { category });
      setShortagePlan(plan);
    } catch (e) {
      setError(e.message);
    }
  };

  // Apply Shortage Plan
  const applyPlan = async () => {
    if (!shortagePlan?.suggestedPlan?.length) return;
    handleAction(async () => {
      const allocations = shortagePlan.suggestedPlan.map(s => ({
        requestId: s.requestId,
        resourceId: s.resourceId,
        quantityAllocated: s.quantityAllocated
      }));
      await post('/resources/shortages/resolve-plan', {
        allocations,
        notes: `Executed automated priority shortage plan for ${shortagePlan.category}`
      });
      setShortagePlan(null);
    }, 'Shortage allocation plan executed successfully. All reservations recorded.');
  };

  // Verification Portal: Fetch Token Preview
  const lookupVerification = async (e) => {
    e?.preventDefault();
    if (!verifyTokenInput.trim()) return;
    setError('');
    try {
      const data = await get(`/delivery-verify/${encodeURIComponent(verifyTokenInput.trim())}`);
      setVerifyPreview(data);
      setVerifyDeliveredQty(data.dispatchedQuantity || 1);
    } catch (e) {
      setError(e.message);
      setVerifyPreview(null);
    }
  };

  // Verification Portal: Confirm
  const submitVerification = async () => {
    if (!verifyPreview) return;
    handleAction(async () => {
      await post(`/delivery-verify/${encodeURIComponent(verifyPreview.shortCode || verifyTokenInput)}/confirm`, {
        deliveredQuantity: Number(verifyDeliveredQty),
        notes: verifyNotes
      });
      setVerifyPreview(null);
      setVerifyTokenInput('');
      setVerifyNotes('');
    }, 'Aid delivery successfully confirmed and verified! Inventory and assistance status reconciled.');
  };

  // Filtered Resources
  const filteredResources = resources.filter(r => {
    if (search && !r.name.toLowerCase().includes(search.toLowerCase()) && !r.category.toLowerCase().includes(search.toLowerCase())) return false;
    if (categoryFilter !== 'All' && r.category !== categoryFilter) return false;
    if (areaFilter !== 'All' && !r.area.toLowerCase().includes(areaFilter.toLowerCase())) return false;
    if (lowStockFilter && r.availableQuantity > 15) return false;
    if (expiringFilter) {
      if (!r.expiresAt) return false;
      const days = (new Date(r.expiresAt) - new Date()) / (1000 * 3600 * 24);
      if (days < 0 || days > 14) return false;
    }
    return true;
  });

  const uniqueAreas = Array.from(new Set(resources.map(r => r.area).filter(Boolean)));
  const categories = ['Food', 'Water', 'Medicine', 'Medical equipment', 'Clothing', 'Shelter materials', 'Hygiene products', 'Transportation supplies', 'Emergency equipment', 'Other'];

  return (
    <>
      <SectionHeading
        kicker="SUPPLY CHAIN / LOGISTICS & DISPATCH MANAGEMENT"
        title="Humanitarian Resource & Logistics Center"
        subtitle="End-to-end inventory control, multi-constraint matching, shortage resolution, dispatch tracking and QR delivery verification"
        action={
          canManage && (
            <div className="btn-group">
              <Button onClick={() => setShowAddModal(true)}><PackagePlus size={16} /> New Resource</Button>
            </div>
          )
        }
      />

      <ErrorMessage error={error} />
      <Success message={success} />

      {/* OPERATIONS DASHBOARD KPI CARDS */}
      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-icon navy"><Layers size={22} /></div>
          <div className="stat-label">Total Resource Types</div>
          <div className="stat-value">{metrics?.totalResourceTypes ?? resources.length}</div>
          <div className="stat-detail">Across all warehouses</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon green"><Boxes size={22} /></div>
          <div className="stat-label">Available Units</div>
          <div className="stat-value">{metrics?.totalAvailableStock ?? resources.reduce((n, r) => n + r.availableQuantity, 0)}</div>
          <div className="stat-detail">Ready for immediate allocation</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon amber"><Clock size={22} /></div>
          <div className="stat-label">Reserved Units</div>
          <div className="stat-value">{metrics?.totalReservedStock ?? resources.reduce((n, r) => n + (r.reservedQuantity || 0), 0)}</div>
          <div className="stat-detail">{metrics?.pendingAllocations ?? 0} cases pending dispatch</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon blue"><Truck size={22} /></div>
          <div className="stat-label">Active Dispatches</div>
          <div className="stat-value">{metrics?.activeDispatches ?? dispatches.filter(d => ['Dispatched', 'InTransit'].includes(d.deliveryStatus)).length}</div>
          <div className="stat-detail">Vehicles in field transit</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon red"><ShieldAlert size={22} /></div>
          <div className="stat-label">Shortage Pressure</div>
          <div className="stat-value">{metrics?.outstandingShortagesCount ?? 0}</div>
          <div className="stat-detail">Categories with unmet demand</div>
        </div>
      </div>

      {/* SUBMODULE NAVIGATION TABS */}
      <div className="tab-bar">
        {[
          { key: 'inventory', label: 'Inventory Control', icon: Boxes },
          { key: 'movements', label: 'Stock Movements Audit', icon: History },
          { key: 'allocations', label: 'Match & Allocate', icon: CheckCircle },
          { key: 'shortages', label: 'Shortage Management', icon: AlertTriangle },
          { key: 'dispatches', label: 'Dispatch & Delivery', icon: Truck },
          { key: 'verification', label: 'QR Delivery Verification', icon: QrCode }
        ].map(t => (
          <button
            key={t.key}
            className={`tab-btn ${tab === t.key ? 'active' : ''}`}
            onClick={() => { setTab(t.key); setError(''); setSuccess(''); }}
          >
            <t.icon size={16} />
            <span>{t.label}</span>
          </button>
        ))}
      </div>

      {/* =========================================================================
          TAB 1: INVENTORY MANAGEMENT
      ========================================================================= */}
      {tab === 'inventory' && (
        <Panel
          title="Physical Inventory & Warehouse Batches"
          subtitle="Real-time stock ledger with condition monitoring, batch tracking and expiry alerts"
          action={
            <div className="filter-bar">
              <div className="search-box">
                <Search size={15} />
                <input
                  type="text"
                  placeholder="Search resources..."
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                />
              </div>

              <select value={categoryFilter} onChange={e => setCategoryFilter(e.target.value)}>
                <option value="All">All Categories</option>
                {categories.map(c => <option key={c} value={c}>{c}</option>)}
              </select>

              <select value={areaFilter} onChange={e => setAreaFilter(e.target.value)}>
                <option value="All">All Depots / Areas</option>
                {uniqueAreas.map(a => <option key={a} value={a}>{a}</option>)}
              </select>

              <label className="checkbox-pill">
                <input
                  type="checkbox"
                  checked={lowStockFilter}
                  onChange={e => setLowStockFilter(e.target.checked)}
                /> Low Stock (≤15)
              </label>

              <label className="checkbox-pill">
                <input
                  type="checkbox"
                  checked={expiringFilter}
                  onChange={e => setExpiringFilter(e.target.checked)}
                /> Expiring Soon
              </label>
            </div>
          }
        >
          <SafeTable
            headers={['ITEM & BATCH', 'CATEGORY', 'WAREHOUSE', 'AVAILABLE / TOTAL', 'CONDITION', 'EXPIRY', 'STATUS', 'ACTIONS']}
            empty="No matching resources found in inventory."
          >
            {filteredResources.map(r => {
              const isLow = r.availableQuantity <= 15;
              const daysToExpiry = r.expiresAt ? Math.round((new Date(r.expiresAt) - new Date()) / (1000 * 3600 * 24)) : null;

              return (
                <tr key={r.id}>
                  <td>
                    <strong>{r.name}</strong>
                    <div className="subtext">
                      Batch: {r.batchNumber || 'N/A'} {r.description && `· ${r.description}`}
                    </div>
                  </td>
                  <td><Pill>{r.category}</Pill></td>
                  <td>
                    <div className="flex-row">
                      <MapPin size={14} /> {r.area}
                    </div>
                  </td>
                  <td>
                    <div className="stock">
                      <strong>{r.availableQuantity}</strong>
                      <span className="unit-label">/ {r.totalQuantity || r.availableQuantity} {r.unit}</span>
                      <Progress value={r.availableQuantity} max={Math.max(r.totalQuantity || 50, r.availableQuantity)} />
                      {r.reservedQuantity > 0 && (
                        <small className="amber-text">({r.reservedQuantity} reserved)</small>
                      )}
                    </div>
                  </td>
                  <td>
                    <span className={`condition-tag ${(r.condition || 'Good').toLowerCase()}`}>
                      {r.condition || 'Good'}
                    </span>
                  </td>
                  <td>
                    {r.expiresAt ? (
                      <span className={daysToExpiry !== null && daysToExpiry <= 14 ? 'red-text' : ''}>
                        {new Date(r.expiresAt).toLocaleDateString()}
                        {daysToExpiry !== null && (
                          <small> ({daysToExpiry > 0 ? `${daysToExpiry}d left` : 'Expired'})</small>
                        )}
                      </span>
                    ) : <span className="subtext">Non-perishable</span>}
                  </td>
                  <td>
                    {isLow ? <Pill tone="red">Low Stock</Pill> : <Pill tone="green">In Stock</Pill>}
                  </td>
                  <td>
                    <div className="btn-group-sm">
                      {canManage && (
                        <>
                          <button
                            className="btn-link"
                            title="Replenish Stock"
                            onClick={() => {
                              setShowReplenishModal(r);
                              setReplenishForm({ quantity: 10, notes: '', batchNumber: r.batchNumber || '', expiresAt: '' });
                            }}
                          >
                            <PackagePlus size={15} /> Replenish
                          </button>
                          <button
                            className="btn-link"
                            title="Adjust / Record Damage"
                            onClick={() => {
                              setShowAdjustModal(r);
                              setAdjustForm({ newQuantity: r.availableQuantity, movementType: 'Stock Adjusted', reason: '' });
                            }}
                          >
                            <ArrowUpDown size={15} /> Adjust
                          </button>
                          <button
                            className="btn-link"
                            title="Edit Details"
                            onClick={() => setShowEditModal(r)}
                          >
                            Edit
                          </button>
                          <button
                            className="btn-link danger"
                            title="Deactivate"
                            onClick={() => handleDeactivate(r)}
                          >
                            Deactivate
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </SafeTable>
        </Panel>
      )}

      {/* =========================================================================
          TAB 2: STOCK MOVEMENTS AUDIT LOG
      ========================================================================= */}
      {tab === 'movements' && (
        <Panel
          title="Inventory Movements Audit Trail"
          subtitle="Immutable chronological ledger of all stock intake, reservations, dispatches, deliveries and adjustments"
        >
          <SafeTable
            headers={['TIMESTAMP', 'MOVEMENT TYPE', 'RESOURCE', 'QUANTITY', 'NOTES & REFERENCE']}
            empty="No stock movements recorded yet."
          >
            {movements.map(m => {
              const res = resources.find(r => r.id === (m.resourceId || m.ResourceId));
              return (
                <tr key={m.id}>
                  <td>{formatDate(m.createdAt)}</td>
                  <td>
                    <Pill tone={
                      m.movementType.includes('Received') ? 'green' :
                      m.movementType.includes('Reserved') ? 'amber' :
                      m.movementType.includes('Dispatched') ? 'blue' :
                      m.movementType.includes('Delivered') ? 'green' :
                      m.movementType.includes('Returned') ? 'blue' : 'red'
                    }>
                      {m.movementType}
                    </Pill>
                  </td>
                  <td>
                    <strong>{res ? res.name : shortId(m.resourceId || m.ResourceId)}</strong>
                    <div className="subtext">{res?.category} · {res?.area}</div>
                  </td>
                  <td>
                    <strong>{m.quantity}</strong> {res?.unit || 'units'}
                  </td>
                  <td>
                    <div>{m.notes || 'Routine inventory movement'}</div>
                    {m.referenceId && (
                      <small className="subtext">Ref ID: {shortId(m.referenceId)}</small>
                    )}
                  </td>
                </tr>
              );
            })}
          </SafeTable>
        </Panel>
      )}

      {/* =========================================================================
          TAB 3: MATCH & ALLOCATE
      ========================================================================= */}
      {tab === 'allocations' && (
        <div className="dash-grid">
          <Panel
            title="Intelligent Resource Matching & Allocation"
            subtitle="Multi-constraint matching algorithm scoring proximity, category, condition, and expiry safety"
          >
            <div className="form-grid">
              <Field label="Select Approved Case / Request">
                <select value={chosenReq} onChange={e => handleSelectRequest(e.target.value)}>
                  <option value="">-- Choose verified assistance request --</option>
                  {requests.map(r => (
                    <option key={r.id} value={r.id}>
                      {shortId(r.id)} · {r.category} ({r.requestedQuantity} units) · Priority: {r.urgency} · {r.peopleAffected} affected
                    </option>
                  ))}
                </select>
              </Field>

              {chosenReq && matches.length > 0 && (
                <div className="match-list">
                  <div className="eyebrow">Ranked Suitable Inventory</div>
                  {matches.map(m => (
                    <button
                      type="button"
                      key={m.id}
                      onClick={() => setChosenRes(m.id)}
                      className={`match ${chosenRes === m.id ? 'active' : ''}`}
                    >
                      <div className="match-info">
                        <strong>{m.name}</strong>
                        <div className="subtext">
                          <MapPin size={13} /> {m.area} ({m.distanceKm} km away) · Available: {m.availableQuantity} {m.unit}
                        </div>
                        <div className="rationale-tag">{m.rationale}</div>
                      </div>
                      <div className="match-score">
                        <span>{Math.round(m.score)} pts</span>
                        <small>{m.availableForFullRequest ? 'Full Match' : `Partial (${m.allocatableQuantity})`}</small>
                      </div>
                    </button>
                  ))}
                </div>
              )}

              {chosenReq && matches.length === 0 && (
                <div className="feedback error">
                  <AlertTriangle size={18} />
                  <span>No suitable inventory found. Check category, accessibility constraints or check the Shortage Management tab.</span>
                </div>
              )}

              <Field label="Units to Reserve & Allocate" hint="Cannot exceed available stock or requested quantity">
                <input
                  type="number"
                  min="1"
                  value={quantity}
                  onChange={e => setQuantity(+e.target.value)}
                />
              </Field>

              {canManage && (
                <Button
                  disabled={!chosenReq || !chosenRes || busy}
                  onClick={submitAllocate}
                >
                  <Check size={16} /> {busy ? 'Allocating...' : 'Approve & Reserve Allocation'}
                </Button>
              )}
            </div>
          </Panel>

          <Panel
            title="Active Reservations Ready for Dispatch"
            subtitle="Approved allocations ready to be assigned to transport vehicles"
          >
            <div className="reservations-list">
              {requests.filter(r => r.status === 'Assigned').length > 0 ? (
                requests.filter(r => r.status === 'Assigned').map(r => (
                  <div className="ops-record" key={r.id}>
                    <div className="ops-icon"><Truck size={20} /></div>
                    <div className="ops-data">
                      <strong>{shortId(r.id)} · {r.category} ({r.requestedQuantity} units)</strong>
                      <span>Urgency: {r.urgency} · {r.peopleAffected} people affected</span>
                      <small>Status: Reserved & Assigned · Awaiting Vehicle Dispatch</small>
                    </div>
                    {canManage && (
                      <div className="record-actions">
                        <Button
                          variant="secondary"
                          onClick={() => {
                            get(`/resources/allocations`).then(allocs => {
                              const found = (allocs || []).find(a => a.requestId === r.id && a.status === 'Reserved');
                              if (found) {
                                setShowDispatchModal({ allocationId: found.id, requestId: r.id, maxQty: found.quantity });
                                setDispatchForm(f => ({ ...f, allocationId: found.id, dispatchedQuantity: found.quantity }));
                              } else {
                                setError('No pending reserved allocation found for this request.');
                              }
                            });
                          }}
                        >
                          <Send size={14} /> Dispatch
                        </Button>
                      </div>
                    )}
                  </div>
                ))
              ) : (
                <Empty text="No approved reservations awaiting dispatch." />
              )}
            </div>
          </Panel>
        </div>
      )}

      {/* =========================================================================
          TAB 4: SHORTAGE MANAGEMENT
      ========================================================================= */}
      {tab === 'shortages' && (
        <Panel
          title="Resource Shortage Resolution & Competing Requests"
          subtitle="Equitable allocation planning when demand exceeds available inventory"
        >
          {shortageData?.categories?.length > 0 ? (
            <div className="shortage-cards-grid">
              {shortageData.categories.map(cat => (
                <div className={`shortage-card ${cat.hasShortage ? 'alert' : 'ok'}`} key={cat.category}>
                  <div className="shortage-header">
                    <h4>{cat.category}</h4>
                    {cat.hasShortage ? (
                      <Pill tone="red">Deficit: {cat.shortageUnits} units</Pill>
                    ) : (
                      <Pill tone="green">Sufficient Supply</Pill>
                    )}
                  </div>

                  <div className="metrics-row">
                    <div>Required: <strong>{cat.totalRequiredUnits}</strong></div>
                    <div>Available: <strong>{cat.totalAvailableUnits}</strong></div>
                    <div>Pending Cases: <strong>{cat.pendingRequestsCount}</strong></div>
                  </div>

                  {cat.competingRequests?.length > 0 && (
                    <div className="competing-section">
                      <div className="eyebrow">Priority Ranked Competing Requests</div>
                      <div className="competing-table">
                        {cat.competingRequests.slice(0, 4).map(cr => (
                          <div className="competing-row" key={cr.id}>
                            <span>{shortId(cr.id)} ({cr.urgency})</span>
                            <span>{cr.requestedQuantity} units</span>
                            <span className="priority-pill">{cr.priorityScore} pts</span>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  {canManage && cat.hasShortage && (
                    <div className="shortage-actions">
                      <Button onClick={() => generatePlan(cat.category)}>
                        Generate Allocation Plan
                      </Button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          ) : (
            <Empty text="No shortage pressure detected across active verified cases." />
          )}

          {/* SUGGESTED SHORTAGE PLAN REVIEW MODAL / SECTION */}
          {shortagePlan && (
            <div className="plan-preview-panel">
              <div className="plan-header">
                <h3>Recommended Shortage Allocation Plan for {shortagePlan.category}</h3>
                <button className="icon-btn" onClick={() => setShortagePlan(null)}><X size={18} /></button>
              </div>
              <p>
                Calculated prioritisation based on urgency, medical emergency and population impact.
                Total demand: {shortagePlan.totalDemand} | Allocatable: {shortagePlan.totalAllocatable} | Unmet: {shortagePlan.unmetShortage}
              </p>

              <SafeTable
                headers={['CASE ID', 'PRIORITY', 'WAREHOUSE BATCH', 'ALLOCATED', 'REMAINING UNMET', 'RATIONALE']}
              >
                {shortagePlan.suggestedPlan.map((s, idx) => (
                  <tr key={idx}>
                    <td><strong>{shortId(s.requestId)}</strong></td>
                    <td><Pill>{s.urgency}</Pill> ({s.priorityWeight} pts)</td>
                    <td>{s.resourceName} ({s.resourceArea})</td>
                    <td><strong className="green-text">{s.quantityAllocated}</strong></td>
                    <td>{s.quantityUnmet > 0 ? <span className="red-text">{s.quantityUnmet} units</span> : 'None'}</td>
                    <td><small>{s.rationale}</small></td>
                  </tr>
                ))}
              </SafeTable>

              {canManage && (
                <div className="plan-actions">
                  <Button onClick={applyPlan} disabled={busy}>
                    <Check size={16} /> Execute & Reserve Allocation Plan
                  </Button>
                  <Button variant="secondary" onClick={() => setShortagePlan(null)}>
                    Cancel
                  </Button>
                </div>
              )}
            </div>
          )}
        </Panel>
      )}

      {/* =========================================================================
          TAB 5: DISPATCH & DELIVERY TRACKING
      ========================================================================= */}
      {tab === 'dispatches' && (
        <Panel
          title="Active Logistics Dispatches & Field Deliveries"
          subtitle="Track vehicle departures, transit status, delivery reconciliation and recipient confirmation"
        >
          <SafeTable
            headers={['DISPATCH ID', 'SUPPLIES & QUANTITY', 'VEHICLE / DRIVER', 'ROUTE', 'STATUS', 'VERIFICATION CODE', 'ACTIONS']}
            empty="No active dispatch records found."
          >
            {dispatches.map(d => (
              <tr key={d.id}>
                <td><strong>{shortId(d.id)}</strong></td>
                <td>
                  <strong>{d.dispatchedQuantity} × {d.itemName}</strong>
                  <div className="subtext">{d.itemCategory}</div>
                </td>
                <td>
                  <div><Truck size={14} /> {d.vehicleId}</div>
                  <div className="subtext">Driver: {d.assignedDriver}</div>
                </td>
                <td>
                  <div>From: {d.departureLocation}</div>
                  <div className="subtext">To: {d.destination}</div>
                </td>
                <td>
                  <Pill tone={
                    d.deliveryStatus === 'Delivered' || d.deliveryStatus === 'Completed' ? 'green' :
                    d.deliveryStatus === 'InTransit' ? 'blue' :
                    d.deliveryStatus === 'PartiallyDelivered' ? 'amber' : 'red'
                  }>
                    {d.deliveryStatus}
                  </Pill>
                </td>
                <td>
                  <div className="qr-code-pill" onClick={() => openQrModal(d.id)}>
                    <QrCode size={14} />
                    <span>{d.verificationShortCode || 'View QR'}</span>
                  </div>
                </td>
                <td>
                  <div className="btn-group-sm">
                    {canManage && d.deliveryStatus === 'ReadyForDispatch' && (
                      <button className="btn-link" onClick={() => handleDispatchStatus(d.id, 'InTransit')}>
                        Start Transit
                      </button>
                    )}
                    {canManage && d.deliveryStatus === 'InTransit' && (
                      <button className="btn-link" onClick={() => handleDispatchStatus(d.id, 'Delivered')}>
                        Confirm Delivered
                      </button>
                    )}
                    <button className="btn-link" onClick={() => openQrModal(d.id)}>
                      <QrCode size={14} /> Show QR
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </SafeTable>
        </Panel>
      )}

      {/* =========================================================================
          TAB 6: QR DELIVERY VERIFICATION PORTAL
      ========================================================================= */}
      {tab === 'verification' && (
        <div className="dash-grid">
          <Panel
            title="Humanitarian Delivery Verification Portal"
            subtitle="Recipient or Field Officer QR code scanning and delivery quantity confirmation"
          >
            <form onSubmit={lookupVerification} className="form-grid">
              <Field
                label="Scan or Enter Verification Code / Token"
                hint="Enter the 8-character shortcode (e.g. 7X9B-4KD2) or paste the URL token"
              >
                <div className="input-group">
                  <input
                    type="text"
                    placeholder="Enter code (e.g. 4KD2-7X9B)..."
                    value={verifyTokenInput}
                    onChange={e => setVerifyTokenInput(e.target.value)}
                  />
                  <Button type="submit"><Search size={16} /> Lookup</Button>
                </div>
              </Field>
            </form>

            {verifyPreview && (
              <div className="verification-card">
                <div className="ver-header">
                  <CheckCircle size={24} className="green-text" />
                  <div>
                    <h4>Verified Humanitarian Consignment</h4>
                    <small>Shortcode: {verifyPreview.shortCode}</small>
                  </div>
                  <Pill tone={verifyPreview.status === 'Verified' ? 'green' : 'amber'}>
                    {verifyPreview.status}
                  </Pill>
                </div>

                <div className="ver-details">
                  <div className="ver-row">
                    <span>Relief Item:</span>
                    <strong>{verifyPreview.itemName} ({verifyPreview.itemCategory})</strong>
                  </div>
                  <div className="ver-row">
                    <span>Dispatched Quantity:</span>
                    <strong>{verifyPreview.dispatchedQuantity} {verifyPreview.itemUnit}</strong>
                  </div>
                  <div className="ver-row">
                    <span>Transport Vehicle:</span>
                    <strong>{verifyPreview.vehicleId} (Driver: {verifyPreview.assignedDriver})</strong>
                  </div>
                  <div className="ver-row">
                    <span>Destination Zone:</span>
                    <strong>{verifyPreview.destinationArea}</strong>
                  </div>
                </div>

                {verifyPreview.status === 'Pending' && !verifyPreview.isExpired && (
                  <div className="ver-confirm-form">
                    <Field label="Confirmed Received Quantity">
                      <input
                        type="number"
                        min="1"
                        max={verifyPreview.dispatchedQuantity}
                        value={verifyDeliveredQty}
                        onChange={e => setVerifyDeliveredQty(+e.target.value)}
                      />
                    </Field>

                    <Field label="Recipient / Field Notes (Optional)">
                      <textarea
                        rows="2"
                        placeholder="Condition of goods, packaging notes, discrepancies..."
                        value={verifyNotes}
                        onChange={e => setVerifyNotes(e.target.value)}
                      />
                    </Field>

                    <Button onClick={submitVerification} disabled={busy}>
                      <Check size={16} /> Confirm Receipt & Invalidate QR Token
                    </Button>
                  </div>
                )}

                {verifyPreview.status === 'Verified' && (
                  <div className="feedback success">
                    <CheckCircle size={18} />
                    <span>This delivery has already been verified and signed off. Replay attack prevented.</span>
                  </div>
                )}
              </div>
            )}
          </Panel>

          <Panel
            title="QR Verification Information"
            subtitle="Security & Privacy Design in Humanitarian Deliveries"
          >
            <div className="info-block">
              <h5>Cryptographically Secure Tokens</h5>
              <p>Each dispatch generates an entropy-rich 192-bit token and a clean human-readable shortcode with single-use invalidation.</p>

              <h5>Privacy Protection</h5>
              <p>The public QR verification link masks vulnerable citizen names and exact phone numbers to prevent privacy leakage in field operations.</p>

              <h5>Automatic Inventory Reconciliation</h5>
              <p>Upon confirmation, stock is marked delivered. Any reported shortage or discrepancy automatically reconciles back to inventory without negative stock errors.</p>
            </div>
          </Panel>
        </div>
      )}

      {/* =========================================================================
          MODALS
      ========================================================================= */}

      {/* ADD RESOURCE MODAL */}
      {showAddModal && (
        <div className="modal-backdrop">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Register New Humanitarian Inventory</h3>
              <button className="icon-btn" onClick={() => setShowAddModal(false)}><X size={18} /></button>
            </div>
            <form onSubmit={submitAddResource} className="form-grid">
              <Field label="Resource Name">
                <input required value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} />
              </Field>
              <Field label="Category">
                <select value={form.category} onChange={e => setForm({ ...form, category: e.target.value })}>
                  {categories.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </Field>
              <Field label="Unit of Measurement">
                <input required value={form.unit} onChange={e => setForm({ ...form, unit: e.target.value })} />
              </Field>
              <Field label="Initial Stock Quantity">
                <input type="number" min="0" required value={form.availableQuantity} onChange={e => setForm({ ...form, availableQuantity: e.target.value })} />
              </Field>
              <Field label="Warehouse / Location Area">
                <input required value={form.area} onChange={e => setForm({ ...form, area: e.target.value })} />
              </Field>
              <Field label="Condition">
                <select value={form.condition} onChange={e => setForm({ ...form, condition: e.target.value })}>
                  <option value="Good">Good / New</option>
                  <option value="Fair">Fair / Usable</option>
                  <option value="Damaged">Damaged / Quarantine</option>
                </select>
              </Field>
              <Field label="Batch Number">
                <input value={form.batchNumber} placeholder="Auto-generated if blank" onChange={e => setForm({ ...form, batchNumber: e.target.value })} />
              </Field>
              <Field label="Expiry Date (Optional)">
                <input type="date" value={form.expiresAt} onChange={e => setForm({ ...form, expiresAt: e.target.value })} />
              </Field>
              <Field label="Description">
                <textarea rows="2" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} />
              </Field>
              <label className="check-row">
                <input type="checkbox" checked={form.accessible} onChange={e => setForm({ ...form, accessible: e.target.checked })} />
                Accessible / Disability Suitable
              </label>
              <div className="modal-footer">
                <Button type="submit"><PackagePlus size={16} /> Save to Inventory</Button>
                <Button variant="secondary" onClick={() => setShowAddModal(false)}>Cancel</Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* REPLENISH MODAL */}
      {showReplenishModal && (
        <div className="modal-backdrop">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Replenish Stock: {showReplenishModal.name}</h3>
              <button className="icon-btn" onClick={() => setShowReplenishModal(null)}><X size={18} /></button>
            </div>
            <form onSubmit={submitReplenish} className="form-grid">
              <Field label={`Quantity to Add (${showReplenishModal.unit})`}>
                <input type="number" min="1" required value={replenishForm.quantity} onChange={e => setReplenishForm({ ...replenishForm, quantity: e.target.value })} />
              </Field>
              <Field label="Batch Number">
                <input value={replenishForm.batchNumber} onChange={e => setReplenishForm({ ...replenishForm, batchNumber: e.target.value })} />
              </Field>
              <Field label="Expiry Date">
                <input type="date" value={replenishForm.expiresAt} onChange={e => setReplenishForm({ ...replenishForm, expiresAt: e.target.value })} />
              </Field>
              <Field label="Intake Notes">
                <textarea rows="2" value={replenishForm.notes} onChange={e => setReplenishForm({ ...replenishForm, notes: e.target.value })} />
              </Field>
              <div className="modal-footer">
                <Button type="submit"><Check size={16} /> Confirm Replenishment</Button>
                <Button variant="secondary" onClick={() => setShowReplenishModal(null)}>Cancel</Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ADJUST STOCK MODAL */}
      {showAdjustModal && (
        <div className="modal-backdrop">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Adjust Inventory: {showAdjustModal.name}</h3>
              <button className="icon-btn" onClick={() => setShowAdjustModal(null)}><X size={18} /></button>
            </div>
            <form onSubmit={submitAdjust} className="form-grid">
              <div className="subtext">Current Available Stock: {showAdjustModal.availableQuantity} {showAdjustModal.unit}</div>
              <Field label="New Corrected Available Quantity">
                <input type="number" min="0" required value={adjustForm.newQuantity} onChange={e => setAdjustForm({ ...adjustForm, newQuantity: e.target.value })} />
              </Field>
              <Field label="Movement / Adjustment Type">
                <select value={adjustForm.movementType} onChange={e => setAdjustForm({ ...adjustForm, movementType: e.target.value })}>
                  <option value="Stock Adjusted">Stock Adjusted (Audit Discrepancy)</option>
                  <option value="Stock Damaged">Stock Damaged / Unusable</option>
                  <option value="Stock Expired">Stock Expired</option>
                  <option value="Stock Returned">Stock Returned to Shelf</option>
                </select>
              </Field>
              <Field label="Audit Reason">
                <textarea required rows="2" value={adjustForm.reason} placeholder="Explain why quantity is adjusted..." onChange={e => setAdjustForm({ ...adjustForm, reason: e.target.value })} />
              </Field>
              <div className="modal-footer">
                <Button type="submit"><Check size={16} /> Save Adjustment</Button>
                <Button variant="secondary" onClick={() => setShowAdjustModal(null)}>Cancel</Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* DISPATCH MODAL */}
      {showDispatchModal && (
        <div className="modal-backdrop">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Dispatch Consignment</h3>
              <button className="icon-btn" onClick={() => setShowDispatchModal(null)}><X size={18} /></button>
            </div>
            <form onSubmit={submitDispatch} className="form-grid">
              <Field label="Assigned Vehicle Number / ID">
                <input required value={dispatchForm.vehicleId} onChange={e => setDispatchForm({ ...dispatchForm, vehicleId: e.target.value })} />
              </Field>
              <Field label="Assigned Driver Name">
                <input required value={dispatchForm.assignedDriver} onChange={e => setDispatchForm({ ...dispatchForm, assignedDriver: e.target.value })} />
              </Field>
              <Field label="Departure Warehouse">
                <input required value={dispatchForm.departureLocation} onChange={e => setDispatchForm({ ...dispatchForm, departureLocation: e.target.value })} />
              </Field>
              <Field label="Destination Relief Center">
                <input required value={dispatchForm.destination} onChange={e => setDispatchForm({ ...dispatchForm, destination: e.target.value })} />
              </Field>
              <Field label={`Quantity to Dispatch (Max: ${showDispatchModal.maxQty})`}>
                <input type="number" min="1" max={showDispatchModal.maxQty} required value={dispatchForm.dispatchedQuantity} onChange={e => setDispatchForm({ ...dispatchForm, dispatchedQuantity: e.target.value })} />
              </Field>
              <Field label="Dispatch Instructions">
                <textarea rows="2" value={dispatchForm.notes} onChange={e => setDispatchForm({ ...dispatchForm, notes: e.target.value })} />
              </Field>
              <div className="modal-footer">
                <Button type="submit"><Send size={16} /> Confirm Departure & Generate QR</Button>
                <Button variant="secondary" onClick={() => setShowDispatchModal(null)}>Cancel</Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* EDIT MODAL */}
      {showEditModal && (
        <div className="modal-backdrop">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Edit Resource Details: {showEditModal.name}</h3>
              <button className="icon-btn" onClick={() => setShowEditModal(null)}><X size={18} /></button>
            </div>
            <form onSubmit={submitEditResource} className="form-grid">
              <Field label="Resource Name">
                <input required value={showEditModal.name} onChange={e => setShowEditModal({ ...showEditModal, name: e.target.value })} />
              </Field>
              <Field label="Category">
                <select value={showEditModal.category} onChange={e => setShowEditModal({ ...showEditModal, category: e.target.value })}>
                  {categories.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </Field>
              <Field label="Unit of Measurement">
                <input required value={showEditModal.unit} onChange={e => setShowEditModal({ ...showEditModal, unit: e.target.value })} />
              </Field>
              <Field label="Warehouse / Location Area">
                <input required value={showEditModal.area} onChange={e => setShowEditModal({ ...showEditModal, area: e.target.value })} />
              </Field>
              <Field label="Condition">
                <select value={showEditModal.condition || 'Good'} onChange={e => setShowEditModal({ ...showEditModal, condition: e.target.value })}>
                  <option value="Good">Good / New</option>
                  <option value="Fair">Fair / Usable</option>
                  <option value="Damaged">Damaged / Quarantine</option>
                </select>
              </Field>
              <Field label="Batch Number">
                <input value={showEditModal.batchNumber || ''} onChange={e => setShowEditModal({ ...showEditModal, batchNumber: e.target.value })} />
              </Field>
              <Field label="Expiry Date">
                <input type="date" value={showEditModal.expiresAt ? showEditModal.expiresAt.split('T')[0] : ''} onChange={e => setShowEditModal({ ...showEditModal, expiresAt: e.target.value })} />
              </Field>
              <Field label="Description">
                <textarea rows="2" value={showEditModal.description || ''} onChange={e => setShowEditModal({ ...showEditModal, description: e.target.value })} />
              </Field>
              <label className="check-row">
                <input type="checkbox" checked={showEditModal.accessible} onChange={e => setShowEditModal({ ...showEditModal, accessible: e.target.checked })} />
                Accessible / Disability Suitable
              </label>
              <div className="modal-footer">
                <Button type="submit"><Check size={16} /> Save Changes</Button>
                <Button variant="secondary" onClick={() => setShowEditModal(null)}>Cancel</Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* QR CODE MODAL */}
      {showQrModal && (
        <div className="modal-backdrop">
          <div className="modal-content qr-modal">
            <div className="modal-header">
              <h3>Delivery Verification QR Code</h3>
              <button className="icon-btn" onClick={() => setShowQrModal(null)}><X size={18} /></button>
            </div>
            <div className="qr-container">
              <div
                className="qr-svg-wrapper"
                dangerouslySetInnerHTML={{ __html: showQrModal.qrCodeSvg }}
              />
              <div className="qr-shortcode">
                Verification Code: <strong>{showQrModal.shortCode}</strong>
              </div>
              <p className="subtext">
                Scan with mobile camera or enter shortcode at the delivery point to verify and confirm receipt.
              </p>
              <Button
                variant="secondary"
                onClick={() => {
                  navigator.clipboard?.writeText(showQrModal.shortCode);
                  setSuccess('Verification shortcode copied to clipboard!');
                }}
              >
                Copy Code
              </Button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
