// The knob panel, built from the schema. It knows nothing about any game: groups, labels,
// units and ranges all come from the [Knob] attributes (or the knobs the game declared in code).

import { escapeHtml } from './lab-text.js';

/**
 * @param host    element to fill
 * @param knobs   schema.knobs
 * @param values  { knobId: value } — mutated as sliders move
 * @param o       { t, fmt, onInput(knob, dragging) }
 * @returns       { repaint() } to call after values were changed from outside
 */
export function buildKnobs(host, knobs, values, o) {
    const { t, fmt } = o;
    host.innerHTML = '';
    const box = document.createElement('div');
    box.className = 'lab-knobs';
    box.innerHTML = `<div class="lab-knobs-head"><h3>${escapeHtml(t.knobs)}</h3><span>
        <button class="lab-btn ghost small" data-act="reset">${escapeHtml(t.resetAll)}</button>
        <button class="lab-btn ghost small" data-act="src">${escapeHtml(t.showFields)}</button></span></div>`;
    host.appendChild(box);

    const inputs = [];
    const groups = [...new Set(knobs.map(k => k.group || ''))];
    for (const group of groups) {
        const section = document.createElement('div');
        section.className = 'lab-kgroup';
        if (group) section.innerHTML = `<h4>${escapeHtml(group)}</h4>`;
        box.appendChild(section);

        for (const knob of knobs.filter(k => (k.group || '') === group)) {
            const physics = knob.layer === 'physics';
            const row = document.createElement('div');
            row.className = 'lab-knob' + (physics ? ' physics' : '');
            row.title = [knob.hint, physics ? t.physicsHint : ''].filter(Boolean).join('\n');
            const source = knob.source
                ? `${knob.source.file} · ${knob.source.dataType}${knob.source.row ? `[${knob.source.row}]` : ''}.${knob.source.property}`
                : knob.id;
            const basePct = (knob.baseline - knob.min) / (knob.max - knob.min) * 100;
            row.innerHTML = `
                <div class="row">
                    <label><span class="changed"></span><span>${escapeHtml(knob.label)}</span>${physics ? ` <span class="lab-badge">${escapeHtml(t.physics)}</span>` : ''}${knob.mixed ? ` <span class="lab-badge">${escapeHtml(t.mixed)}</span>` : ''}</label>
                    <span class="val"><span class="num" data-v></span><small>${escapeHtml(knob.unit)}</small><button class="reset" title="${escapeHtml(t.resetKnob)}">↺</button></span>
                </div>
                <div class="lab-slider">
                    <input type="range" min="${knob.min}" max="${knob.max}" step="${knob.step}" aria-label="${escapeHtml(knob.label)}" ${knob.editable ? '' : 'disabled'}>
                    ${basePct >= 0 && basePct <= 100 ? `<span class="tick" style="left:calc(8px + (100% - 16px) * ${basePct / 100})"></span>` : ''}
                </div>
                <div class="was">${escapeHtml(t.was)} ${fmt.knob(knob, knob.baseline)}</div>
                <div class="src">${escapeHtml(source)}</div>`;
            section.appendChild(row);

            const input = row.querySelector('input'), shown = row.querySelector('[data-v]');
            const paint = () => {
                const v = values[knob.id];
                input.value = v;
                shown.textContent = fmt.knob(knob, v);
                input.style.setProperty('--fill', Math.max(0, Math.min(100, (v - knob.min) / (knob.max - knob.min) * 100)) + '%');
                row.classList.toggle('is-changed', Math.abs(v - knob.baseline) > 1e-9);
            };
            input.addEventListener('input', () => { values[knob.id] = +input.value; paint(); o.onInput(knob, true); });
            input.addEventListener('change', () => o.onInput(knob, false));
            row.querySelector('.reset').addEventListener('click', () => { values[knob.id] = knob.baseline; paint(); o.onInput(knob, false); });
            paint();
            inputs.push(paint);
        }
    }

    box.querySelector('[data-act="src"]').addEventListener('click', ev => {
        const on = box.classList.toggle('show-src');
        ev.target.textContent = on ? t.hideFields : t.showFields;
    });
    box.querySelector('[data-act="reset"]').addEventListener('click', () => {
        for (const knob of knobs) values[knob.id] = knob.baseline;
        inputs.forEach(paint => paint());
        o.onInput(null, false);
    });

    return { repaint() { inputs.forEach(paint => paint()); } };
}
