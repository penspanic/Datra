// The lab's views. Framework-free: every view is a function (container, data) that draws
// SVG into the container. A view sees only the result document — stages, progression points,
// purchases — never the game, so the same code draws a live run, a stored snapshot, or a
// result baked into a standalone page.

const NS = 'http://www.w3.org/2000/svg';

/** Left gutter shared by the progression and the timeline, so their time axes line up. */
const GUTTER = 96;

export function el(tag, attrs, parent) {
    const e = document.createElementNS(NS, tag);
    for (const k in attrs || {}) if (attrs[k] !== undefined && attrs[k] !== null) e.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(e);
    return e;
}

function text(parent, x, y, s, attrs) {
    const t = el('text', Object.assign({ x, y }, attrs || {}), parent);
    t.textContent = s;
    return t;
}

function lin(d0, d1, r0, r1) {
    const f = v => r0 + (v - d0) / ((d1 - d0) || 1) * (r1 - r0);
    f.inv = px => d0 + (px - r0) / ((r1 - r0) || 1) * (d1 - d0);
    return f;
}

function ticks(min, max, n) {
    const span = max - min || 1;
    const raw = span / (n || 5);
    const mag = Math.pow(10, Math.floor(Math.log10(raw)));
    const step = [1, 2, 2.5, 5, 10].map(s => s * mag).find(s => s >= raw) || raw;
    const out = [];
    for (let v = Math.ceil(min / step) * step; v <= max + step * 1e-9; v += step) out.push(+v.toFixed(10));
    return out;
}

/** Colours are read from the host's --lab-* tokens so a theme override reaches the SVG too. */
function palette(container) {
    const style = getComputedStyle(container.closest('.datra-lab') || container);
    const get = name => style.getPropertyValue(name).trim();
    return {
        ink: get('--lab-ink') || '#1d2127',
        ink3: get('--lab-ink-3') || '#858c95',
        line: get('--lab-line') || '#e1e4e8',
        line2: get('--lab-line-2') || '#cfd4da',
        panel: get('--lab-panel') || '#ffffff',
        accent: get('--lab-accent') || '#2f6bd8',
        wash: get('--lab-accent-wash') || 'rgba(47,107,216,0.12)',
        base: get('--lab-base') || '#7a828c',
    };
}

let tipEl;
export function tip(html, ev) {
    if (!tipEl) {
        tipEl = document.createElement('div');
        tipEl.className = 'datra-lab-tip';
        document.body.appendChild(tipEl);
    }
    if (html == null) { tipEl.style.opacity = 0; return; }
    tipEl.innerHTML = html;
    tipEl.style.opacity = 1;
    const r = tipEl.getBoundingClientRect();
    let x = ev.clientX + 14, y = ev.clientY + 14;
    if (x + r.width > innerWidth - 8) x = ev.clientX - r.width - 14;
    if (y + r.height > innerHeight - 8) y = ev.clientY - r.height - 14;
    tipEl.style.left = x + 'px';
    tipEl.style.top = y + 'px';
}

export function removeTip() {
    if (tipEl) { tipEl.remove(); tipEl = null; }
}

function mount(container, h) {
    container.innerHTML = '';
    const w = Math.max(240, container.clientWidth);
    const svg = el('svg', { viewBox: `0 0 ${w} ${h}`, width: w, height: h, role: 'img' }, container);
    return { svg, w, h };
}

/** A column with a 4px rounded data end and a square foot on the baseline. */
function roundTopRect(parent, x, y, w, h, r, attrs) {
    if (h <= 0) return null;
    r = Math.min(r, w / 2, h);
    const d = `M${x},${y + h}V${y + r}Q${x},${y} ${x + r},${y}H${x + w - r}Q${x + w},${y} ${x + w},${y + r}V${y + h}Z`;
    return el('path', Object.assign({ d }, attrs), parent);
}

// ---------- time per stage: columns, an 80% whisker, a target tick; optional comparison pairs ----------
/**
 * @param o.stages   [{ id, label, median, p10, p90, targetSeconds }] (seconds)
 * @param o.compare  the same, for the scenario compared with (matched by id), or null
 * @param o.t, o.fmt strings and formatters
 */
export function stageBars(container, o) {
    const { svg, w, h } = mount(container, o.height || 240);
    const c = palette(container);
    const m = { l: 36, r: 8, t: 28, b: 36 };
    const stages = o.stages;
    const compare = o.compare ? stages.map(s => o.compare.find(b => b.id === s.id) || null) : null;
    const minutes = v => v == null ? null : v / 60;

    const tops = [];
    stages.forEach((s, i) => {
        tops.push(minutes(s.p90 ?? s.median), minutes(s.targetSeconds));
        if (compare && compare[i]) tops.push(minutes(compare[i].p90 ?? compare[i].median));
    });
    const ymax = Math.max(1, ...tops.filter(v => v != null)) * 1.14;
    const y = lin(0, ymax, h - m.b, m.t);

    for (const tk of ticks(0, ymax, 4)) {
        el('line', { x1: m.l, x2: w - m.r, y1: y(tk), y2: y(tk), stroke: c.line, 'shape-rendering': 'crispEdges' }, svg);
        text(svg, m.l - 8, y(tk) + 4, tk, { 'text-anchor': 'end' });
    }
    text(svg, m.l - 8, m.t - 14, o.t.minutes, { 'text-anchor': 'end' });

    const band = (w - m.l - m.r) / Math.max(1, stages.length);
    stages.forEach((s, i) => {
        const cx = m.l + band * (i + 0.5);
        const group = el('g', {}, svg);
        // A hit area wider than the marks, so hovering anywhere in the column works.
        el('rect', { x: cx - band / 2, y: m.t - 20, width: band, height: h - m.t + 20, fill: 'transparent' }, group);
        const bw = Math.min(compare ? 22 : 30, band * 0.3);
        const base = compare ? compare[i] : null;

        const drawBar = (d, x, color, label) => {
            if (d.median == null) return;
            const top = y(minutes(d.median));
            roundTopRect(group, x - bw / 2, top, bw, y(0) - top, 4, { fill: color });
            if (d.p10 != null && d.p90 != null) {
                const lo = y(minutes(d.p10)), hi = y(minutes(d.p90));
                for (const [x1, x2, y1, y2] of [[x, x, lo, hi], [x - 4, x + 4, hi, hi], [x - 4, x + 4, lo, lo]])
                    el('line', { x1, x2, y1, y2, stroke: c.ink, 'stroke-width': 1.2, opacity: 0.45 }, group);
            }
            if (label) text(group, x, Math.min(top, d.p90 != null ? y(minutes(d.p90)) : 1e9) - 7, label, { 'text-anchor': 'middle', class: 'lbl num' });
        };

        if (base) {
            // 2px of surface between the pair so the two fills never touch.
            drawBar(base, cx - bw / 2 - 1, c.base);
            drawBar(s, cx + bw / 2 + 1, c.accent);
            if (s.median != null && base.median != null) {
                const dv = (s.median - base.median) / 60;
                text(group, cx, m.t - 8, o.fmt.signed(dv), { 'text-anchor': 'middle', class: Math.abs(dv) < 0.05 ? '' : 'lbl-strong num' });
            }
        } else {
            drawBar(s, cx, c.accent, s.median == null ? '–' : o.fmt.minBare(s.median));
        }

        if (s.targetSeconds != null) {
            const tw = base ? bw * 2 + 16 : bw + 16;
            const ty = y(minutes(s.targetSeconds));
            el('line', { x1: cx - tw / 2, x2: cx + tw / 2, y1: ty, y2: ty, stroke: c.ink, 'stroke-width': 2, 'stroke-dasharray': '3 2' }, group);
        }

        text(svg, cx, h - m.b + 16, s.id, { 'text-anchor': 'middle', class: 'lbl' });
        if (s.targetSeconds != null && s.median != null) {
            const over = (s.median - s.targetSeconds) / 60;
            const off = Math.abs(over) > Math.max(0.5, s.targetSeconds / 60 * 0.1);
            text(svg, cx, h - m.b + 30, off ? `${o.t.target} ${o.fmt.signed(over)}` : `${o.t.target} ${o.fmt.minBare(s.targetSeconds, 0)}`,
                { 'text-anchor': 'middle', class: off ? 'lbl-warn' : '' });
        }

        group.addEventListener('mousemove', ev => {
            const rows = [`<b>${esc(s.label || s.id)}</b>`, o.t.tipStage(o.fmt.min(s.median), s.targetSeconds != null ? o.fmt.min(s.targetSeconds) : '')];
            if (s.p10 != null) rows.push(`<span class="m">${o.t.tipRange(o.fmt.minBare(s.p10), o.fmt.min(s.p90))}</span>`);
            if (base && base.median != null) rows.push(`<span class="m">${esc(o.compareName)}: ${o.fmt.min(base.median)}</span>`);
            tip(rows.join('<br>'), ev);
        });
        group.addEventListener('mouseleave', () => tip(null));
    });
}

// ---------- progression: which stage the bots are in, and how far through it, over time ----------
/**
 * @param o.stages  [{ id, targetSeconds }] top to bottom
 * @param o.series  [{ points: [{ t, p10, median, p90 }], role: 'now' | 'base', band, endLabel, name }]
 * @param o.tMax    seconds
 */
export function progression(container, o) {
    const { svg, w, h } = mount(container, o.height || 290);
    const c = palette(container);
    const m = { l: GUTTER, r: 56, t: 12, b: 30 };
    const n = o.stages.length;
    const tmax = Math.max(1, o.tMax / 60);
    const x = lin(0, tmax, m.l, w - m.r), y = lin(0, n, m.t, h - m.b);

    for (let k = 0; k <= n; k++)
        el('line', { x1: m.l, x2: w - m.r, y1: y(k), y2: y(k), stroke: c.line, 'shape-rendering': 'crispEdges' }, svg);
    o.stages.forEach((s, k) => text(svg, m.l - 10, y(k + 0.5) + 4, s.id, { 'text-anchor': 'end', class: 'lbl' }));
    text(svg, m.l - 10, y(n) + 4, o.t.end, { 'text-anchor': 'end', class: 'lbl' });
    for (const tk of ticks(0, tmax, 8)) {
        text(svg, x(tk), h - m.b + 18, tk + (tk === 0 ? '' : o.fmt.gap + o.t.minutes), { 'text-anchor': 'middle' });
        el('line', { x1: x(tk), x2: x(tk), y1: h - m.b, y2: h - m.b + 4, stroke: c.line2 }, svg);
    }

    // Target pace: each stage done in exactly its target time.
    if (o.stages.every(s => s.targetSeconds != null)) {
        let cum = 0;
        const pts = [[x(0), y(0)]];
        o.stages.forEach((s, i) => { cum += s.targetSeconds / 60; if (cum <= tmax * 1.001) pts.push([x(cum), y(i + 1)]); });
        el('polyline', { points: pts.map(p => p.join(',')).join(' '), fill: 'none', stroke: c.ink3, 'stroke-width': 1.5, 'stroke-dasharray': '4 4' }, svg);
    }

    for (const s of o.series) {
        const G = s.points.filter(p => p.t / 60 <= tmax * 1.0001);
        if (G.length === 0) continue;
        const color = s.role === 'base' ? c.base : c.accent;
        if (s.band) {
            const top = G.map(p => `${x(p.t / 60)},${y(p.p10)}`).join(' ');
            const bottom = G.slice().reverse().map(p => `${x(p.t / 60)},${y(p.p90)}`).join(' ');
            el('polygon', { points: top + ' ' + bottom, fill: c.wash }, svg);
        }
        el('polyline', { points: G.map(p => `${x(p.t / 60)},${y(p.median)}`).join(' '), fill: 'none', stroke: color, 'stroke-width': 2, 'stroke-linejoin': 'round' }, svg);
        const last = G.find(p => p.median >= n - 1e-6) || G[G.length - 1];
        el('circle', { cx: x(last.t / 60), cy: y(last.median), r: 4.5, fill: color, stroke: c.panel, 'stroke-width': 2 }, svg);
        if (s.endLabel) text(svg, x(last.t / 60) + 8, y(last.median) - 8, s.endLabel, { 'text-anchor': 'start', class: 'lbl-strong num' });
    }

    const cross = el('line', { y1: m.t, y2: h - m.b, stroke: c.ink, 'stroke-width': 1, opacity: 0 }, svg);
    const hit = el('rect', { x: m.l, y: m.t, width: w - m.l - m.r, height: h - m.t - m.b, fill: 'transparent' }, svg);
    hit.addEventListener('mousemove', ev => {
        const r = svg.getBoundingClientRect();
        const px = (ev.clientX - r.left) * (w / r.width);
        const tm = x.inv(px);
        cross.setAttribute('x1', px); cross.setAttribute('x2', px); cross.setAttribute('opacity', 0.35);
        const rows = o.series.map(s => {
            let p = s.points[0];
            for (const q of s.points) { if (q.t / 60 > tm) break; p = q; }
            if (!p) return '';
            const k = Math.min(n - 1, Math.floor(p.median));
            const where = p.median >= n - 1e-6 ? o.t.tipDone : o.t.tipStageFill(esc(o.stages[k].id), Math.round((p.median - k) * 100));
            return `${esc(s.name)}: <b>${where}</b>`;
        });
        tip(`<span class="m">${tm.toFixed(1)}${o.fmt.gap}${o.t.minutes}</span><br>${rows.join('<br>')}`, ev);
    });
    hit.addEventListener('mouseleave', () => { cross.setAttribute('opacity', 0); tip(null); });
}

// ---------- purchase timeline: one dot per purchase, a lane per group ----------
/**
 * @param o.lanes      [{ label, role: 'now' | 'base', items: [{ t, label, cost, stage }] }] (t in seconds)
 * @param o.stageEnds  [{ id, t }] of the representative bot
 */
export function timeline(container, o) {
    const laneH = 26, pad = 8;
    const { svg, w, h } = mount(container, pad * 2 + Math.max(1, o.lanes.length) * laneH + 22);
    const c = palette(container);
    const m = { l: GUTTER, r: 56, t: pad, b: 22 };
    const tmax = Math.max(1, o.tMax / 60);
    const x = lin(0, tmax, m.l, w - m.r);

    // The label names the stage the bot enters, so the last end (the finish) gets none.
    (o.stageEnds || []).forEach((end, i) => {
        if (i >= o.stageEnds.length - 1 || end.t / 60 > tmax) return;
        const px = x(end.t / 60);
        el('line', { x1: px, x2: px, y1: m.t, y2: h - m.b, stroke: c.line2, 'stroke-dasharray': '2 3' }, svg);
        const next = o.stageEnds[i + 1];
        if (next) text(svg, px + 4, h - m.b + 14, '↓ ' + next.id, { style: 'font-size:10.5px' });
    });

    // Lane labels can be longer than the gutter: measure, then trim with an ellipsis.
    const fit = (node, max) => {
        let s = node.textContent;
        try {
            while (s.length > 1 && node.getComputedTextLength() > max) { s = s.slice(0, -1); node.textContent = s + '…'; }
        } catch (e) { /* not laid out (hidden tab): leave the text as it is */ }
    };

    o.lanes.forEach((lane, li) => {
        const cy = m.t + laneH * (li + 0.5);
        el('line', { x1: m.l, x2: w - m.r, y1: cy, y2: cy, stroke: c.line }, svg);
        const color = lane.role === 'base' ? c.base : c.accent;
        const label = text(svg, m.l - 10, cy + 4, lane.label, { 'text-anchor': 'end', class: lane.role === 'base' ? '' : 'lbl' });
        const title = el('title', {}, label); title.textContent = lane.label;
        fit(label, m.l - 14);
        for (const it of lane.items) {
            if (it.t / 60 > tmax) continue;
            const r = Math.max(4, Math.min(7, 2.4 + Math.log10(it.cost + 1) * 0.9));
            const dot = el('circle', { cx: x(it.t / 60), cy, r, fill: color, stroke: c.panel, 'stroke-width': 2 }, svg);
            const hit = el('circle', { cx: x(it.t / 60), cy, r: r + 5, fill: 'transparent' }, svg);
            hit.addEventListener('mousemove', ev => {
                dot.setAttribute('r', r + 1.5);
                tip(`<b>${esc(it.label)}</b><br>${(it.t / 60).toFixed(1)}${o.fmt.gap}${o.t.minutes} · ${esc(it.stage)}<br><span class="m">${o.t.tipCost} ${o.fmt.number(it.cost)}</span>`, ev);
            });
            hit.addEventListener('mouseleave', () => { dot.setAttribute('r', r); tip(null); });
        }
    });
}

function esc(value) {
    return String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
}
