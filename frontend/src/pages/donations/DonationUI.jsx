import { useState } from 'react';

export function DataForm({ fields, onSubmit, button = 'Save' }) {
  const [form, setForm] = useState({});
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(e) {
    e.preventDefault();
    setError('');
    setSuccess('');
    setBusy(true);

    try {
      await onSubmit(form);
      setSuccess('Saved successfully.');
      setForm({});
    } catch (err) {
      setError(err.message || 'Operation failed.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="member05-form">
      {fields.map(field => (
        <label key={field.name}>
          <strong>{field.label}</strong>

          {field.options ? (
            <select
              required={!field.optional}
              value={form[field.name] ?? ''}
              onChange={e => setForm({
                ...form,
                [field.name]: e.target.value
              })}
            >
              <option value="">Select an option</option>

              {field.options.map(option => (
                <option
                  key={option.value}
                  value={option.value}
                >
                  {option.label}
                </option>
              ))}
            </select>
          ) : (
            <input
              type={field.type || 'text'}
              step={field.step}
              min={field.min}
              required={!field.optional}
              value={form[field.name] ?? ''}
              onChange={e => setForm({
                ...form,
                [field.name]: e.target.value
              })}
            />
          )}
        </label>
      ))}

      {error && <p role="alert">{error}</p>}
      {success && <p role="status">{success}</p>}

      <button className="btn primary" disabled={busy}>
        {busy ? 'Please wait...' : button}
      </button>
    </form>
  );
}

export function DataTable({ rows, columns }) {
  if (!rows.length) {
    return <p>No records available.</p>;
  }

  return (
    <div className="member05-table">
      <table>
        <thead>
          <tr>
            {columns.map(column => (
              <th key={column}>{column}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              {row.map((cell, i) => (
                <td key={i}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export const field = (name, label, extra = {}) => ({
  name, label, ...extra
});

export const option = (value, label = value) => ({
  value, label
});

export const categories = [
  'Food', 'Water', 'Medicine',
  'Transport', 'Shelter', 'Other'
].map(value => option(value));