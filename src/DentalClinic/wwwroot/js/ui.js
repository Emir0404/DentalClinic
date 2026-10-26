// Small behaviours that used to be written inline in the pages (onclick="...", onsubmit="...").
// They live here so the Content-Security-Policy can forbid inline scripts altogether.
(() => {
  // <form data-confirm="Question?">: ask before submitting
  document.addEventListener('submit', (e) => {
    const message = e.target.dataset?.confirm;
    if (message && !window.confirm(message)) e.preventDefault();
  });

  document.addEventListener('click', (e) => {
    // <button data-print>: print the page
    if (e.target.closest('[data-print]')) { window.print(); return; }

    // <tr data-href="/somewhere">: the whole table row is a link (but real links and buttons inside still work)
    const row = e.target.closest('tr[data-href]');
    if (row && !e.target.closest('a, button, input, select, textarea, label')) window.location.href = row.dataset.href;
  });

  // <select data-preview-target="imgId" data-preview-prefix="/img/" data-preview-suffix=".jpg">: show the chosen picture
  document.addEventListener('change', (e) => {
    const select = e.target.closest('select[data-preview-target]');
    if (!select) return;
    const image = document.getElementById(select.dataset.previewTarget);
    if (image) image.src = (select.dataset.previewPrefix ?? '') + select.value + (select.dataset.previewSuffix ?? '');
  });
})();
