// Text of the lab screen itself, and number formatting. Knob labels, stage names and
// explanations come from the game and are shown as they are.

const STRINGS = {
    en: {
        loading: 'Loading the lab…',
        knobs: 'Knobs',
        showFields: 'Show data fields',
        hideFields: 'Hide data fields',
        physics: 'physics',
        physicsHint: 'Reaches the physics layer. Read-only until stored outcomes can be recomputed.',
        resetKnob: 'Back to the saved value',
        resetAll: 'Reset',
        was: 'saved',
        mixed: 'rows differ',
        bots: 'Bots',
        botSettings: 'Bots',
        policy: 'Policy',
        seed: 'Seed',
        botHint: 'Both sides of a comparison play with the same seeds, so only the knobs differ.',
        compare: 'Compare with',
        savedData: 'Saved data',
        applyKnobs: 'Use its knobs',
        saveSnapshot: 'Save snapshot',
        snapshotName: 'Snapshot name',
        snapshotSaved: name => `Saved snapshot "${name}".`,
        upToDate: 'Up to date',
        running: 'Running bots…',
        failed: 'Run failed',
        heroCap: (bots, finished) => finished === bots
            ? `to play through, median of <b>${bots}</b> bots`
            : `to play through, median of the <b>${finished}</b> of ${bots} bots that finished`,
        noneFinished: 'No bot finished',
        sameAs: name => `same as ${name}`,
        versus: (name, delta) => `${delta} vs ${name}`,
        firstPurchase: 'First purchase',
        purchases: 'Purchases',
        perBot: 'per bot, median',
        noPurchases: 'nothing bought',
        target: 'target',
        computed: 'This run',
        computedUnit: 'h played',
        computedIn: ms => `by the bots in ${ms} ms`,
        stageTitle: 'Time per stage',
        stageSub: 'Bar: median across bots. Thin line: the middle 80%. Dashed: target.',
        whyTitle: 'Against the targets',
        whySub: 'Written from the result. Reasons in italics come from the game’s simulator.',
        progTitle: 'Progression: where the bots are',
        progSub: 'Line: median. Band: the middle 80%. Dashed: the target pace.',
        timelineTitle: 'Purchase timeline',
        timelineSub: 'One representative bot (the one closest to the median). Dot size: price.',
        legendNow: 'This scenario',
        legendTarget: 'Target',
        end: 'End',
        table: 'Table',
        stage: 'Stage',
        median: 'Median',
        range: 'Middle 80%',
        over: (id, d, t) => `<b>${id} runs ${d} over its target</b> of ${t}.`,
        under: (id, d, t) => `<b>${id} runs ${d} under its target</b> of ${t}.`,
        allOnTarget: 'Every stage is within reach of its target.',
        noTargets: 'No stage has a target yet.',
        checkFailed: (name, value, target) => `<b>${name} is ${value}.</b> Target: ${target}.`,
        unfinished: n => `<b>${n} bots were still playing at the time cap.</b> Their last stage has no length.`,
        missingOutcome: (key, n) => `<b>No stored physics outcomes for <code>${key}</code>.</b> ${n} bots stopped there.`,
        botError: (message, n) => `<b>${n} bots stopped with an error:</b> ${message}`,
        changedKnobs: n => n === 1 ? '1 knob moved' : `${n} knobs moved`,
        baselineMoved: 'The saved data has changed since this snapshot was taken.',
        minutes: 'min',
        seconds: 's',
        unitGap: ' ',
        tipStage: (now, target) => `median ${now}${target ? ` · target ${target}` : ''}`,
        tipRange: (lo, hi) => `80% of bots: ${lo}–${hi}`,
        tipCost: 'price',
        tipDone: 'finished',
        tipStageFill: (id, pct) => `${id} · ${pct}% of the way`,
        footer: (context, hash, outcomes) => `${context} · baseline ${hash}${outcomes ? ` · physics outcomes ${outcomes}` : ''}`,
    },
    ko: {
        loading: '실험실을 불러오는 중…',
        knobs: '손잡이',
        showFields: '표 필드 보기',
        hideFields: '표 필드 숨기기',
        physics: '물리',
        physicsHint: '물리 층에 닿는 값입니다. 저장된 물리 결과를 다시 계산할 수 있을 때까지 읽기 전용입니다.',
        resetKnob: '저장된 값으로',
        resetAll: '되돌리기',
        was: '저장값',
        mixed: '행마다 다름',
        bots: '봇',
        botSettings: '봇',
        policy: '정책',
        seed: '시드',
        botHint: '비교하는 양쪽이 같은 시드로 돌기 때문에 차이는 손잡이에서만 옵니다.',
        compare: '비교 대상',
        savedData: '저장된 데이터',
        applyKnobs: '이 손잡이 값 쓰기',
        saveSnapshot: '스냅샷 저장',
        snapshotName: '스냅샷 이름',
        snapshotSaved: name => `스냅샷 "${name}" 을 저장했습니다.`,
        upToDate: '최신',
        running: '봇을 돌리는 중…',
        failed: '실행 실패',
        heroCap: (bots, finished) => finished === bots
            ? `완주 시간, 봇 <b>${bots}</b>명 중앙값`
            : `완주 시간, 봇 ${bots}명 중 끝까지 간 <b>${finished}</b>명의 중앙값`,
        noneFinished: '끝까지 간 봇이 없음',
        sameAs: name => `${name} 와 같음`,
        versus: (name, delta) => `${name} 대비 ${delta}`,
        firstPurchase: '첫 구매',
        purchases: '구매 수',
        perBot: '봇 한 명, 중앙값',
        noPurchases: '구매 없음',
        target: '목표',
        computed: '이번 계산',
        computedUnit: '시간 분량',
        computedIn: ms => `봇 플레이를 ${ms} ms 에`,
        stageTitle: '단계마다 걸리는 시간',
        stageSub: '막대는 봇 중앙값, 가는 선은 80% 범위, 점선은 목표.',
        whyTitle: '목표와 견주면',
        whySub: '결과에서 뽑은 문장. 기울인 이유는 게임의 시뮬레이터가 적은 것.',
        progTitle: '진행: 언제 어느 단계에 있나',
        progSub: '선은 봇 중앙값, 띠는 80% 범위, 점선은 목표 속도.',
        timelineTitle: '구매 타임라인',
        timelineSub: '대표 봇 한 명(완주 시간이 중앙값에 가장 가까운 봇). 점 크기는 가격.',
        legendNow: '지금 시나리오',
        legendTarget: '목표',
        end: '끝',
        table: '표',
        stage: '단계',
        median: '중앙값',
        range: '80% 범위',
        over: (id, d, t) => `<b>${id} 가 목표 ${t}보다 ${d} 길다.</b>`,
        under: (id, d, t) => `<b>${id} 는 목표 ${t}보다 ${d} 짧다.</b>`,
        allOnTarget: '모든 단계가 목표 근처다.',
        noTargets: '목표가 있는 단계가 없다.',
        checkFailed: (name, value, target) => `<b>${name} ${value}.</b> 목표는 ${target}.`,
        unfinished: n => `<b>봇 ${n}명이 시간 상한까지 끝내지 못했다.</b> 마지막 단계는 길이가 없다.`,
        missingOutcome: (key, n) => `<b><code>${key}</code> 의 저장된 물리 결과가 없다.</b> 봇 ${n}명이 거기서 멈췄다.`,
        botError: (message, n) => `<b>봇 ${n}명이 오류로 멈췄다:</b> ${message}`,
        changedKnobs: n => `손잡이 ${n}개 바꿈`,
        baselineMoved: '이 스냅샷을 찍은 뒤로 저장된 데이터가 바뀌었다.',
        minutes: '분',
        seconds: '초',
        unitGap: '',
        tipStage: (now, target) => `중앙값 ${now}${target ? ` · 목표 ${target}` : ''}`,
        tipRange: (lo, hi) => `봇 80%가 ${lo}–${hi}`,
        tipCost: '가격',
        tipDone: '완주',
        tipStageFill: (id, pct) => `${id} · ${pct}%`,
        footer: (context, hash, outcomes) => `${context} · 기준 ${hash}${outcomes ? ` · 물리 결과 ${outcomes}` : ''}`,
    },
};

export function strings(locale) {
    const key = (locale || 'en').toLowerCase().slice(0, 2);
    return STRINGS[key] || STRINGS.en;
}

export function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

/** Decimals a knob's step calls for: step 0.05 shows two, step 50 shows none. */
export function decimalsFor(step) {
    if (!(step > 0) || step >= 1) return 0;
    return Math.min(4, Math.ceil(-Math.log10(step) - 1e-9));
}

export function formatters(t) {
    const number = (v, d = 0) => Number(v).toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
    // English sets a number off from its unit ("4.0 min"); Korean does not ("4.0분").
    const gap = t.unitGap;
    return {
        number,
        gap,
        knob: (knob, v) => number(v, decimalsFor(knob.step)),
        /** Seconds shown as minutes. */
        min: (seconds, d = 1) => (seconds == null || !isFinite(seconds) ? '–' : (seconds / 60).toFixed(d)) + gap + t.minutes,
        minBare: (seconds, d = 1) => (seconds == null || !isFinite(seconds) ? '–' : (seconds / 60).toFixed(d)),
        sec: seconds => (seconds == null || !isFinite(seconds) ? '–' : Math.round(seconds)) + gap + t.seconds,
        signed: (v, d = 1) => (v > 0 ? '+' : v < 0 ? '−' : '±') + Math.abs(v).toFixed(d),
        value: (v, unit) => {
            if (v == null || !isFinite(v)) return '–';
            const d = Math.abs(v) >= 100 || Number.isInteger(v) ? 0 : 1;
            return number(v, d) + (unit ? gap + unit : '');
        },
    };
}
