(() => {
  const form = document.getElementById('bookingForm');
  if (!form) return;

  const $ = (id) => document.getElementById(id);
  const initial = JSON.parse(form.dataset.initial || '{}');
  const MAX_DAYS = parseInt(form.dataset.maxDays || '60', 10);
  const DAY_NAMES = ['ned', 'pon', 'uto', 'sri', 'čet', 'pet', 'sub'];
  const MONTHS = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'avg', 'sep', 'okt', 'nov', 'dec'];
  const FULL_DAYS = ['nedjelja', 'ponedjeljak', 'utorak', 'srijeda', 'četvrtak', 'petak', 'subota'];

  const state = { service: null, doctor: 'any', doctorName: 'Prvi slobodan doktor', date: '', time: '', loadedDays: 0, token: 0 };

  const parseDate = (s) => { const [y, m, d] = s.split('-').map(Number); return new Date(y, m - 1, d); };
  const fmtLong = (s) => { const d = parseDate(s); return `${FULL_DAYS[d.getDay()]}, ${d.getDate()}. ${MONTHS[d.getMonth()]}`; };
  const get = async (url) => { const r = await fetch(url); if (!r.ok) throw new Error(r.status); return r.json(); };
  const show = (id) => { $(id).hidden = false; };
  const hide = (id) => { $(id).hidden = true; };

  function updateSummary() {
    const s = state.service;
    $('sService').textContent = s ? s.name : '–';
    $('sDoctor').textContent = s ? state.doctorName : '–';
    $('sDate').textContent = state.date ? fmtLong(state.date) : '–';
    $('sTime').textContent = state.time ? state.time + ' h' : '–';
    $('sDuration').textContent = s ? s.duration : '–';
    $('sPrice').textContent = s ? s.price : '–';
    $('fDate').value = state.date;
    $('fTime').value = state.time;
    $('submitBtn').disabled = !(s && state.date && state.time);
  }

  // ---------- korak 1: usluga ----------
  async function chooseService(radio, restore) {
    state.service = { slug: radio.value, name: radio.dataset.name, duration: radio.dataset.duration, price: radio.dataset.price };
    state.doctor = 'any'; state.doctorName = 'Prvi slobodan doktor'; state.date = ''; state.time = '';
    updateSummary();
    show('step2'); show('step3'); show('step4');
    await loadDoctors(restore?.doctor);
    await loadDays(0, true, restore?.date, restore?.time);
    if (!restore) $('step2').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  // ---------- korak 2: doktor ----------
  async function loadDoctors(preselect) {
    const box = $('doctorPick');
    box.innerHTML = '<span class="loading">Učitavam doktore…</span>';
    let doctors = [];
    try { doctors = await get(`/zakazivanje/doktori?usluga=${encodeURIComponent(state.service.slug)}`); } catch { /* prikaži samo "bilo koji" */ }

    box.innerHTML = '';
    const mk = (value, label, small, img, checked) => {
      const l = document.createElement('label');
      l.className = 'pick';
      l.innerHTML = `<input type="radio" name="Doctor" value="${value}" ${checked ? 'checked' : ''}>` +
        (img ? `<img src="${img}" alt="">` : '<span class="any"><svg class="ic"><use href="#i-users"/></svg></span>') +
        `<span><b></b><small></small></span>`;
      l.querySelector('b').textContent = label;
      l.querySelector('small').textContent = small;
      box.appendChild(l);
    };
    const want = doctors.some((d) => d.slug === preselect) ? preselect : 'any';
    mk('any', 'Prvi slobodan', 'najbrži termin', null, want === 'any');
    doctors.forEach((d) => mk(d.slug, d.name, d.title, d.photo, want === d.slug));
    state.doctor = want;
    state.doctorName = want === 'any' ? 'Prvi slobodan doktor' : doctors.find((d) => d.slug === want).name;
    updateSummary();
  }

  // ---------- korak 3: datum i vrijeme ----------
  async function loadDays(offset, reset, preDate, preTime) {
    const token = ++state.token;
    const days = $('days');
    if (reset) { days.innerHTML = '<span class="loading">Učitavam termine…</span>'; state.loadedDays = 0; offset = 0; $('times').innerHTML = ''; hide('timesLabel'); }
    const from = new Date(); from.setHours(0, 0, 0, 0); from.setDate(from.getDate() + offset);
    const count = Math.min(42, MAX_DAYS + 1 - offset);
    if (count <= 0) return;
    const iso = `${from.getFullYear()}-${String(from.getMonth() + 1).padStart(2, '0')}-${String(from.getDate()).padStart(2, '0')}`;
    let list = [];
    try { list = await get(`/zakazivanje/dani?usluga=${state.service.slug}&doktor=${state.doctor}&od=${iso}&broj=${count}`); }
    catch { days.innerHTML = '<span class="loading">Termini trenutno nisu dostupni. Pozovite nas.</span>'; return; }
    if (token !== state.token) return;          // u međuvremenu je promijenjen izbor, odbaci stari odgovor

    if (reset) days.innerHTML = '';
    list.forEach((d) => {
      const dt = parseDate(d.date);
      const b = document.createElement('button');
      b.type = 'button'; b.className = 'day'; b.dataset.date = d.date;
      b.setAttribute('aria-pressed', 'false');
      b.disabled = d.free === 0;
      b.innerHTML = `<small>${DAY_NAMES[dt.getDay()]} · ${MONTHS[dt.getMonth()]}</small><b>${dt.getDate()}</b><em>${d.free === 0 ? 'nema' : d.free + (d.free === 1 ? ' termin' : ' termina')}</em>`;
      days.appendChild(b);
    });
    state.loadedDays = offset + list.length;
    $('moreDays').hidden = state.loadedDays > MAX_DAYS;

    if (preDate) {
      const btn = days.querySelector(`[data-date="${preDate}"]`);
      if (btn && !btn.disabled) await chooseDate(btn, preTime);
    } else if (!list.some((d) => d.free > 0) && reset) {
      $('slotHint').textContent = 'Nema slobodnih termina u idućim sedmicama. Probajte drugog doktora ili nas nazovite.';
    } else if (reset) {
      $('slotHint').textContent = 'Odaberite datum da vidite slobodna vremena.';
    }
  }

  async function chooseDate(btn, preTime) {
    $('days').querySelectorAll('.day').forEach((x) => x.setAttribute('aria-pressed', 'false'));
    btn.setAttribute('aria-pressed', 'true');
    state.date = btn.dataset.date; state.time = '';
    updateSummary();
    const box = $('times');
    box.innerHTML = '<span class="loading">Učitavam vremena…</span>';
    let slots = [];
    try { slots = await get(`/zakazivanje/termini?usluga=${state.service.slug}&doktor=${state.doctor}&datum=${state.date}`); } catch { /* prazno */ }
    box.innerHTML = '';
    show('timesLabel');
    $('slotHint').textContent = slots.length ? 'Odaberite vrijeme dolaska.' : 'Za ovaj dan nema slobodnih vremena. Odaberite drugi datum.';
    slots.forEach((s) => {
      const t = document.createElement('button');
      t.type = 'button'; t.className = 'time'; t.textContent = s.time; t.dataset.time = s.time; t.dataset.doctor = s.doctor;
      t.setAttribute('aria-pressed', 'false');
      box.appendChild(t);
    });
    if (preTime) { const t = box.querySelector(`[data-time="${preTime}"]`); if (t) chooseTime(t); }
  }

  function chooseTime(btn) {
    $('times').querySelectorAll('.time').forEach((x) => x.setAttribute('aria-pressed', 'false'));
    btn.setAttribute('aria-pressed', 'true');
    state.time = btn.dataset.time;
    // kod "prvi slobodan" pokazujemo koji doktor je trenutno predviđen (server ga ponovo bira pri potvrdi)
    if (state.doctor === 'any') state.doctorName = btn.dataset.doctor;
    updateSummary();
    $('step4').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  // ---------- događaji ----------
  form.addEventListener('change', async (e) => {
    if (e.target.name === 'Service') await chooseService(e.target);
    else if (e.target.name === 'Doctor') {
      state.doctor = e.target.value;
      state.doctorName = e.target.value === 'any' ? 'Prvi slobodan doktor' : e.target.closest('label').querySelector('b').textContent;
      state.date = ''; state.time = '';
      updateSummary();
      await loadDays(0, true);
    }
  });

  form.addEventListener('click', async (e) => {
    const day = e.target.closest('.day');
    if (day && !day.disabled) return chooseDate(day);
    const time = e.target.closest('.time');
    if (time) return chooseTime(time);
    if (e.target.id === 'moreDays') { e.target.disabled = true; await loadDays(state.loadedDays, false); e.target.disabled = false; }
  });

  // ponovo prikaži izbor ako je forma vraćena s greškom (ili je usluga došla iz linka)
  const checked = form.querySelector('[name="Service"]:checked');
  if (checked) chooseService(checked, initial);
  updateSummary();
})();
