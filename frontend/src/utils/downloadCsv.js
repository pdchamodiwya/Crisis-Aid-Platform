export function downloadCsv(filename, rows) {
  const escape = value => {
    let text = String(value ?? '');

    if (/^[\s]*[=+\-@]/.test(text)) {
      text = "'" + text;
    }

    return '"' + text.replaceAll('"', '""') + '"';
  };

  const csv = rows
    .map(row => row.map(escape).join(','))
    .join('\r\n');

  const blob = new Blob(
    ['\uFEFF', csv],
    { type: 'text/csv;charset=utf-8;' }
  );

  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');

  link.href = url;
  link.download = filename;
  link.click();

  setTimeout(() => URL.revokeObjectURL(url), 1000);
}