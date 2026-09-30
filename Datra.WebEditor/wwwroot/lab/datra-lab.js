// Datra.Lab screen: the entry point <DatraLab /> imports.
//
//   mount(host, { basePath, locale })   draw the lab into `host`
//   unmount(host)                       remove it again
//
// The screen gets everything through a "source": schema(), run(scenario), and the snapshot
// calls. Today that is httpSource (the MapDatraLab endpoints). A standalone export can hand in
// a source that answers from documents embedded in the page — the views below stay as they are.

import { strings, formatters, escapeHtml } from './lab-text.js';
import { stageBars, progression, timeline, tip, removeTip } from './lab-views.js';
import { buildKnobs } from './lab-knobs.js';

const mounted = new WeakMap();

export function httpSource(basePath) {
    const base = (basePath || '/api/datra/lab').replace(/\/$/, '');
    const call = async (path, body) => {
        const response = await fetch(base + path, body === undefined ? undefined : {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        let data = null;
        try { data = await response.json(); } catch (e) { /* an empty or non-JSON body */ }
        if (!response.ok) throw new Error((data && data.error) || `${response.status} ${response.statusText}`);
        return data;
    };
    return {
        schema: () => call('/schema'),
        run: scenario => call('/run', scenario),
        snapshots: () => call('/snapshots'),
        snapshot: name => call('/snapshots/' + encodeURIComponent(name)),
        saveSnapshot: (name, scenario) => call('/snapshots', { name, scenario }),
    };
}

function ensureStyles() {
    const href = new URL('./datra-lab.css', import.meta.url).href;
    if ([...document.styleSheets].some(s => s.href === href) || document.querySelector(`link[data-datra-lab]`)) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    link.dataset.datraLab = '';
    document.head.appendChild(link);
}

export async function mount(host, options) {
    options = options || {};
    unmount(host);
    ensureStyles();

    const t = strings(options.locale || document.documentElement.lang);
    const fmt = formatters(t);
    const source = options.source || httpSource(options.basePath);
    const state = { disposed: false };
    mounted.set(host, state);

    host.classList.add('datra-lab');
    host.innerHTML = `<div class="lab-loading">${escapeHtml(t.loading)}</div>`;

    let schema;
    try {
        schema = await source.schema();
    } catch (error) {
        if (!state.disposed) host.innerHTML = `<div class="lab-error">${escapeHtml(error.message)}</div>`;
        return;
    }
    if (state.disposed) return;

    // ---- state ----
    const values = Object.fromEntries(schema.knobs.map(k => [k.id, k.baseline]));
    const bot = {
        bots: schema.defaults.bots,
        seed: schema.defaults.seed,
        policy: schema.defaults.policy,
        params: Object.fromEntries(schema.policyParams.map(p => [p.id, p.default])),
    };
    let result = null;          // the scenario on screen
    let saved = null;           // the saved data under the same bot settings
    let savedKey = '';
    let snapshot = null;        // the snapshot compared with, if one is picked
    let snapshots = [];

    const changes = () => Object.fromEntries(schema.knobs
        .filter(k => k.editable && Math.abs(values[k.id] - k.baseline) > 1e-9)
        .map(k => [k.id, values[k.id]]));
    const botSettings = () => ({ bots: bot.bots, seed: bot.seed, policy: bot.policy, policyParams: { ...bot.params } });
    const scenario = () => ({ changes: changes(), ...botSettings() });

    // ---- skeleton ----
    host.innerHTML = `
        <header class="lab-top">
            <div class="lab-brand"><span class="lab-mark"><i></i></span><span data-id="title"></span></div>
            <div class="lab-spacer"></div>
            <span class="lab-chip" data-id="status"><span class="dot"></span><span data-id="statusText"></span></span>
            <span class="lab-field" data-id="compareBox">${escapeHtml(t.compare)} <select data-id="compare"></select>
                <button class="lab-btn small" data-id="apply" hidden>${escapeHtml(t.applyKnobs)}</button></span>
            <span class="lab-field" data-id="saveBox"><input type="text" data-id="snapName" placeholder="${escapeHtml(t.snapshotName)}" maxlength="80">
                <button class="lab-btn small primary" data-id="save">${escapeHtml(t.saveSnapshot)}</button></span>
        </header>
        <div class="lab-page">
            <aside class="lab-rail">
                <div class="lab-box">
                    <h3>${escapeHtml(t.botSettings)}</h3>
                    <div class="rows" data-id="bot"></div>
                    <div class="hint">${escapeHtml(t.botHint)}</div>
                </div>
                <div data-id="knobs"></div>
            </aside>
            <main class="lab-main">
                <section class="lab-panel">
                    <div class="lab-hero">
                        <div><div class="lab-big num" data-id="hero">–</div><div class="lab-cap"><span data-id="heroCap"></span> <span class="lab-delta" data-id="heroDelta"></span></div></div>
                        <div class="lab-tiles" data-id="tiles"></div>
                    </div>
                </section>
                <section class="lab-grid-2">
                    <div class="lab-panel">
                        <div class="lab-ph"><div><h2>${escapeHtml(t.stageTitle)}</h2><div class="lab-sub">${escapeHtml(t.stageSub)}</div></div>
                            <button class="lab-btn ghost small" data-id="tableBtn">${escapeHtml(t.table)}</button></div>
                        <div class="lab-legend" data-id="stageLegend"></div>
                        <div class="lab-chart" data-id="stages"></div>
                        <div data-id="stageTable" hidden></div>
                    </div>
                    <div class="lab-panel">
                        <div class="lab-ph"><div><h2>${escapeHtml(t.whyTitle)}</h2><div class="lab-sub">${escapeHtml(t.whySub)}</div></div></div>
                        <div class="lab-notes" data-id="notes"></div>
                    </div>
                </section>
                <section class="lab-panel">
                    <div class="lab-ph"><div><h2>${escapeHtml(t.progTitle)}</h2><div class="lab-sub">${escapeHtml(t.progSub)}</div></div>
                        <div class="lab-legend" data-id="progLegend"></div></div>
                    <div class="lab-chart" data-id="prog"></div>
                    <div class="lab-ph" style="margin:16px 0 4px"><div><h2 style="font-size:14px">${escapeHtml(t.timelineTitle)}</h2><div class="lab-sub">${escapeHtml(t.timelineSub)}</div></div></div>
                    <div class="lab-chart" data-id="timeline"></div>
                </section>
                <div class="lab-foot" data-id="foot"></div>
            </main>
        </div>`;
    const $ = id => host.querySelector(`[data-id="${id}"]`);
    $('title').textContent = schema.title;

    // ---- bot settings ----
    const maxBots = schema.defaults.maxBots || 400;
    const botRows = [];
    if (schema.policies.length > 1) {
        botRows.push(`<label>${escapeHtml(t.policy)}</label><select data-bot="policy">${schema.policies.map(p =>
            `<option value="${escapeHtml(p.id)}" title="${escapeHtml(p.hint)}">${escapeHtml(p.label)}</option>`).join('')}</select>`);
    }
    botRows.push(`<label>${escapeHtml(t.bots)}</label><input type="number" data-bot="bots" min="1" max="${maxBots}" step="1" value="${bot.bots}">`);
    botRows.push(`<label>${escapeHtml(t.seed)}</label><input type="number" data-bot="seed" step="1" value="${bot.seed}">`);
    for (const p of schema.policyParams) {
        botRows.push(`<label>${escapeHtml(p.label)}${p.unit ? ` (${escapeHtml(p.unit)})` : ''}</label>
            <input type="number" data-param="${escapeHtml(p.id)}" min="${p.min}" max="${p.max}" step="${p.step}" value="${p.default}">`);
    }
    $('bot').innerHTML = botRows.join('');
    $('bot').addEventListener('change', ev => {
        const target = ev.target;
        if (target.dataset.bot === 'policy') bot.policy = target.value;
        else if (target.dataset.bot) {
            const n = Math.round(+target.value);
            if (Number.isFinite(n)) bot[target.dataset.bot] = target.dataset.bot === 'bots' ? Math.max(1, Math.min(maxBots, n)) : n;
            target.value = bot[target.dataset.bot];
        } else if (target.dataset.param) {
            const p = schema.policyParams.find(q => q.id === target.dataset.param);
            const n = +target.value;
            bot.params[p.id] = Number.isFinite(n) ? Math.max(p.min, Math.min(p.max, n)) : p.default;
            target.value = bot.params[p.id];
        }
        schedule();
    });

    // ---- knobs ----
    const knobPanel = buildKnobs($('knobs'), schema.knobs, values, { t, fmt, onInput: () => schedule() });

    // ---- snapshots ----
    const compareSelect = $('compare');
    const paintCompare = () => {
        compareSelect.innerHTML = `<option value="">${escapeHtml(t.savedData)}</option>` +
            snapshots.map(s => `<option value="${escapeHtml(s.name)}">${escapeHtml(s.name)}</option>`).join('');
        compareSelect.value = snapshot ? snapshot.name : '';
        $('apply').hidden = !snapshot;
    };
    const loadSnapshots = async () => {
        if (!schema.snapshots) { $('compareBox').hidden = true; $('saveBox').hidden = true; return; }
        try { snapshots = await source.snapshots(); } catch (e) { snapshots = []; }
        if (!state.disposed) paintCompare();
    };
    compareSelect.addEventListener('change', async () => {
        const name = compareSelect.value;
        try { snapshot = name ? await source.snapshot(name) : null; } catch (error) { snapshot = null; setStatus('bad', error.message); }
        if (state.disposed) return;
        paintCompare();
        render();
    });
    $('apply').addEventListener('click', () => {
        if (!snapshot) return;
        for (const knob of schema.knobs) {
            const v = snapshot.scenario.changes ? snapshot.scenario.changes[knob.id] : undefined;
            values[knob.id] = v === undefined ? knob.baseline : v;
        }
        knobPanel.repaint();
        schedule();
    });
    $('save').addEventListener('click', async () => {
        const name = $('snapName').value.trim();
        if (!name) { $('snapName').focus(); return; }
        try {
            await source.saveSnapshot(name, scenario());
            $('snapName').value = '';
            await loadSnapshots();
            setStatus('', t.snapshotSaved(name));
        } catch (error) {
            setStatus('bad', error.message);
        }
    });
    $('tableBtn').addEventListener('click', () => { $('stageTable').hidden = !$('stageTable').hidden; });

    // ---- running ----
    function setStatus(kind, message) {
        const chip = $('status');
        chip.className = 'lab-chip' + (kind ? ' ' + kind : '');
        $('statusText').textContent = message;
    }

    // One request at a time, and always a last one for the latest knob position: dragging a
    // slider asks for a run on every input event, which would otherwise pile up.
    let running = false, again = false;
    function schedule() {
        if (running) { again = true; return; }
        run();
    }
    async function run() {
        running = true;
        again = false;
        // A run usually returns within a frame or two. Only say "running" and dim the charts
        // when it does not, or the screen flickers on every step of a drag.
        const slow = setTimeout(() => {
            if (state.disposed) return;
            setStatus('busy', t.running);
            host.classList.add('is-stale');
        }, 250);
        try {
            const key = JSON.stringify(botSettings());
            if (!saved || savedKey !== key) {
                saved = await source.run({ changes: {}, ...botSettings() });
                savedKey = key;
            }
            const now = scenario();
            result = Object.keys(now.changes).length === 0 ? saved : await source.run(now);
            if (state.disposed) return;
            clearTimeout(slow);
            host.classList.remove('is-stale');
            setStatus('', t.upToDate);
            render();
        } catch (error) {
            if (state.disposed) return;
            clearTimeout(slow);
            setStatus('bad', `${t.failed}: ${error.message}`);
        } finally {
            clearTimeout(slow);
            running = false;
            if (again && !state.disposed) run();
        }
    }

    // ---- drawing ----
    function comparison() {
        const moved = Object.keys(changes()).length;
        if (snapshot) return { name: snapshot.name, result: snapshot.result, show: true };
        return { name: t.savedData, result: saved, show: moved > 0 };
    }

    function notes(cmp) {
        const list = [];
        for (const p of result.problems || []) {
            if (p.kind === 'unfinished') list.push({ html: t.unfinished(p.bots) });
            else if (p.kind === 'missingOutcome') list.push({ html: t.missingOutcome(escapeHtml(p.message), p.bots) });
            else list.push({ html: t.botError(escapeHtml(p.message), p.bots) });
        }
        if (snapshot && snapshot.baselineHash && snapshot.baselineHash !== schema.baselineHash)
            list.push({ kind: 'info', html: escapeHtml(t.baselineMoved) });

        let targets = 0;
        for (const s of result.stages) {
            if (s.targetSeconds == null || s.median == null) continue;
            targets++;
            const d = s.median - s.targetSeconds;
            const tolerance = Math.max(30, s.targetSeconds * 0.1);
            if (Math.abs(d) <= tolerance) continue;
            const why = (result.explains || []).filter(e => e.stage === s.id).map(e => ` <i>${escapeHtml(e.text)}</i>`).join('');
            const args = [escapeHtml(s.id), fmt.min(Math.abs(d)), fmt.min(s.targetSeconds)];
            list.push({ html: (d > 0 ? t.over(...args) : t.under(...args)) + why });
        }
        for (const c of result.checks || []) {
            if (c.ok === false) list.push({ html: t.checkFailed(escapeHtml(c.name), fmt.value(c.value, c.unit), escapeHtml(c.target + (c.unit ? fmt.gap + c.unit : ''))) });
        }
        if (list.length === 0) list.push({ kind: 'ok', html: escapeHtml(targets > 0 ? t.allOnTarget : t.noTargets) });
        return list;
    }

    function render() {
        if (!result || state.disposed) return;
        const cmp = comparison();
        const total = result.total;

        $('hero').innerHTML = total ? `${fmt.minBare(total.median)}<small>${escapeHtml(t.minutes)}</small>` : '–';
        $('heroCap').innerHTML = total ? t.heroCap(result.bots, result.finished) : escapeHtml(t.noneFinished);
        const delta = $('heroDelta');
        if (cmp.show && total && cmp.result && cmp.result.total) {
            const d = (total.median - cmp.result.total.median) / 60;
            delta.textContent = '· ' + (Math.abs(d) < 0.05 ? t.sameAs(cmp.name) : t.versus(cmp.name, fmt.signed(d) + fmt.gap + t.minutes));
        } else {
            delta.textContent = '';
        }

        // tiles: first purchase, the game's checks, and what this run cost to compute
        const tiles = [];
        const firstCheck = (result.checks || []).find(c => /first purchase|첫 구매/i.test(c.name));
        if (!firstCheck) {
            tiles.push({ l: t.firstPurchase, v: result.purchases.firstSeconds == null ? '–' : fmt.minBare(result.purchases.firstSeconds), u: result.purchases.firstSeconds == null ? '' : t.minutes, d: result.purchases.firstSeconds == null ? t.noPurchases : '' });
        }
        for (const c of result.checks || []) {
            tiles.push({ l: c.name, v: c.value == null ? '–' : fmt.value(c.value), u: c.unit, d: `${t.target} ${c.target}${c.unit ? fmt.gap + c.unit : ''}`, warn: c.ok === false });
        }
        tiles.push({ l: t.purchases, v: fmt.value(result.purchases.countMedian), u: '', d: t.perBot });
        if (result.timing) {
            tiles.push({ l: t.computed, v: (result.timing.simulatedSeconds / 3600).toFixed(1), u: t.computedUnit, d: t.computedIn(Math.max(1, Math.round(result.timing.elapsedMs))) });
        }
        $('tiles').innerHTML = tiles.slice(0, 4).map(x => `<div class="lab-tile ${x.warn ? 'warn' : ''}"><div class="l">${escapeHtml(x.l)}</div><div class="v">${escapeHtml(x.v)}<small>${escapeHtml(x.u || '')}</small></div><div class="d">${escapeHtml(x.d || '')}</div></div>`).join('');

        // time per stage
        const legend = [`<span><i class="sq" style="background:var(--lab-accent)"></i>${escapeHtml(t.legendNow)}</span>`];
        if (cmp.show) legend.push(`<span><i class="sq" style="background:var(--lab-base)"></i>${escapeHtml(cmp.name)}</span>`);
        legend.push(`<span><i class="dash"></i>${escapeHtml(t.legendTarget)}</span>`);
        $('stageLegend').innerHTML = legend.join('');
        $('progLegend').innerHTML = legend.map(s => s.replace('class="sq"', '')).join('');
        stageBars($('stages'), { stages: result.stages, compare: cmp.show && cmp.result ? cmp.result.stages : null, compareName: cmp.name, t, fmt });

        const cmpStage = id => cmp.show && cmp.result ? cmp.result.stages.find(s => s.id === id) : null;
        $('stageTable').innerHTML = `<table class="lab-table"><thead><tr><th>${escapeHtml(t.stage)}</th><th>${escapeHtml(t.median)}</th><th>${escapeHtml(t.range)}</th><th>${escapeHtml(t.target)}</th>${cmp.show ? `<th>${escapeHtml(cmp.name)}</th>` : ''}</tr></thead><tbody>` +
            result.stages.map(s => `<tr><td>${escapeHtml(s.label || s.id)}</td><td>${fmt.min(s.median)}</td><td>${fmt.minBare(s.p10)}–${fmt.minBare(s.p90)}</td><td>${s.targetSeconds == null ? '–' : fmt.min(s.targetSeconds)}</td>${cmp.show ? `<td>${fmt.min((cmpStage(s.id) || {}).median)}</td>` : ''}</tr>`).join('') + '</tbody></table>';

        $('notes').innerHTML = notes(cmp).map(n => `<div class="lab-note ${n.kind || ''}"><span class="ic">${n.kind === 'ok' ? '✓' : n.kind === 'info' ? 'i' : '!'}</span><div>${n.html}</div></div>`).join('');

        // progression and purchases share one time axis
        const tMax = Math.max(result.progression.tMax, cmp.show && cmp.result ? cmp.result.progression.tMax : 0);
        const series = [];
        if (cmp.show && cmp.result) series.push({ name: cmp.name, points: cmp.result.progression.points, role: 'base', band: false });
        series.push({ name: t.legendNow, points: result.progression.points, role: 'now', band: true, endLabel: total ? fmt.min(total.median) : '' });
        progression($('prog'), { stages: result.stages, series, tMax, t, fmt });

        // A lane per purchase group; when comparing, the other scenario's lane sits right under it.
        const lanes = [];
        const other = cmp.show && cmp.result ? cmp.result.purchases : null;
        const groups = [...new Set([...result.purchases.groups, ...(other ? other.groups : [])])];
        for (const group of groups) {
            lanes.push({ label: group || '–', role: 'now', items: result.purchases.items.filter(i => i.group === group) });
            if (other) lanes.push({ label: cmp.name, role: 'base', items: other.items.filter(i => i.group === group) });
        }
        timeline($('timeline'), { lanes, stageEnds: result.purchases.stageEnds, tMax, t, fmt });

        const moved = Object.keys(changes()).length;
        $('foot').textContent = [t.footer(schema.context, schema.baselineHash.slice(0, 8), schema.outcomeVersion), moved ? t.changedKnobs(moved) : ''].filter(Boolean).join(' · ');
    }

    let resizeTimer;
    const onResize = () => { clearTimeout(resizeTimer); resizeTimer = setTimeout(render, 120); };
    window.addEventListener('resize', onResize);
    state.dispose = () => {
        window.removeEventListener('resize', onResize);
        clearTimeout(resizeTimer);
        tip(null);
        removeTip();
    };

    setStatus('busy', t.running);
    await loadSnapshots();
    schedule();
}

export function unmount(host) {
    const state = mounted.get(host);
    if (!state) return;
    state.disposed = true;
    if (state.dispose) state.dispose();
    mounted.delete(host);
    host.innerHTML = '';
}
