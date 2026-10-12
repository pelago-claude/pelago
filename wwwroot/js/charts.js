// Grafici ECharts per Pelago. Le pagine Blazor passano i dati già pronti (nomi, colori, serie):
// qui si decide solo l'aspetto e si costruiscono i tooltip.
window.DPCharts = (function () {
    const registry = new Map();
    // Lingua dei numeri e dei testi dei grafici: la sceglie l'app (setLocale), qui c'è solo l'italiano di partenza.
    let locale = 'it-IT';
    let money, moneyAxis, pct, pctFmt, pct1;
    const S = {
        goal: 'Obiettivo', gap: 'Scostamento', net: 'Versamenti netti', total: 'Totale',
        above: 'Sopra i versamenti', below: 'Sotto i versamenti', base: 'Totale (fino ai versamenti)',
    };
    function buildFormats() {
        money = new Intl.NumberFormat(locale, { style: 'currency', currency: 'EUR', useGrouping: 'always' });
        moneyAxis = new Intl.NumberFormat(locale, { style: 'currency', currency: 'EUR', maximumFractionDigits: 0, useGrouping: 'always' });
        pct = new Intl.NumberFormat(locale, { style: 'percent', minimumFractionDigits: 1, maximumFractionDigits: 1 });
        pctFmt = new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 0 });
        pct1 = pct;
    }
    buildFormats();
    const eur = v => (v === null || v === undefined) ? '' : money.format(v);
    const pctAxis = v => pctFmt.format(v);
    // Numero semplice (cambi, rendimenti): senza simbolo di valuta, con i decimali richiesti.
    const plain = (v, dec, suffix) => (v === null || v === undefined) ? '' : Number(v).toLocaleString(locale, { minimumFractionDigits: dec ?? 4, maximumFractionDigits: dec ?? 4, useGrouping: 'always' }) + (suffix || '');
    const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();

    function theme() {
        return {
            text: css('--text'), muted: css('--muted'), border: css('--border'), card: css('--card'),
            pos: css('--pos'), neg: css('--neg'),
        };
    }

    const dot = c => `<span style="display:inline-block;width:9px;height:9px;border-radius:50%;background:${c};margin-right:6px"></span>`;
    const sign = v => (v >= 0 ? '+' : '−') + money.format(Math.abs(v));

    function baseOption(t, labels, extra) {
        return Object.assign({
            animation: true,
            animationDuration: 400,
            textStyle: { fontFamily: 'Instrument Sans, Segoe UI Variable, Segoe UI, sans-serif', color: t.text },
            grid: { left: 12, right: 24, top: 44, bottom: labels && labels.length > 40 ? 64 : 28, containLabel: true },
            legend: { top: 4, textStyle: { color: t.text }, icon: 'roundRect' },
            xAxis: {
                type: 'category', data: labels, boundaryGap: true,
                axisLine: { lineStyle: { color: t.border } }, axisLabel: { color: t.muted, hideOverlap: true },
                axisTick: { show: false },
            },
            yAxis: {
                type: 'value', axisLabel: { color: t.muted, formatter: v => moneyAxis.format(v) },
                splitLine: { lineStyle: { color: t.border } },
            },
            tooltip: {
                trigger: 'axis', axisPointer: { type: 'shadow' }, confine: true,
                backgroundColor: t.card, borderColor: t.border, textStyle: { color: t.text },
            },
            dataZoom: labels && labels.length > 40
                ? [{ type: 'inside' }, { type: 'slider', height: 18, bottom: 6, borderColor: t.border, textStyle: { color: t.muted } }]
                : [],
        }, extra);
    }

    const builders = {
        donut(d, t) {
            return {
                animationDuration: 500,
                textStyle: { fontFamily: 'Instrument Sans, Segoe UI Variable, Segoe UI, sans-serif', color: t.text },
                tooltip: {
                    trigger: 'item', confine: true, backgroundColor: t.card, borderColor: t.border, textStyle: { color: t.text },
                    formatter: p => `${dot(p.color)}${p.name}<br/><b>${eur(p.value)}</b> &nbsp;(${p.percent.toLocaleString(locale)}%)`,
                },
                legend: { bottom: 0, textStyle: { color: t.text }, icon: 'circle' },
                graphic: [
                    { type: 'text', left: 'center', top: '38%', style: { text: d.centerLabel || '', fill: t.muted, fontSize: 13, textAlign: 'center' } },
                    { type: 'text', left: 'center', top: '46%', style: { text: d.centerValue || '', fill: t.text, fontSize: 20, fontWeight: 700, textAlign: 'center' } },
                ],
                series: [{
                    type: 'pie', radius: ['56%', '80%'], center: ['50%', '45%'], avoidLabelOverlap: true,
                    itemStyle: { borderColor: t.card, borderWidth: 3, borderRadius: 6 },
                    label: { show: false },
                    data: d.items.filter(i => i.value > 0).map(i => ({ name: i.name, value: i.value, itemStyle: { color: i.color } })),
                }],
            };
        },

        lines(d, t) {
            return baseOption(t, d.dates, {
                tooltip: {
                    trigger: 'axis', confine: true, backgroundColor: t.card, borderColor: t.border, textStyle: { color: t.text },
                    valueFormatter: v => d.percent ? pct1.format(v) : d.plain ? plain(v, d.decimals, d.suffix) : eur(v),
                },
                xAxis: {
                    type: 'category', data: d.dates, boundaryGap: false,
                    axisLine: { lineStyle: { color: t.border } }, axisLabel: { color: t.muted, hideOverlap: true },
                    axisTick: { show: false },
                },
                yAxis: {
                    type: 'value', scale: !!d.scale,
                    // con un obiettivo l'asse deve arrivare a contenerlo
                    max: d.goal ? (v => Math.max(v.max, d.goal * 1.03)) : undefined,
                    axisLabel: { color: t.muted, formatter: v => d.percent ? pctAxis(v) : d.plain ? plain(v, d.decimals, d.suffix) : moneyAxis.format(v) },
                    splitLine: { lineStyle: { color: t.border } },
                },
                series: d.series.map((s, i) => ({
                    name: s.name, type: 'line', data: s.values, showSymbol: !!d.symbols, symbolSize: 6, smooth: false, connectNulls: true,
                    lineStyle: { width: s.width || 2, color: s.color === 'text' ? t.text : s.color, type: s.dashed ? 'dashed' : 'solid' },
                    itemStyle: { color: s.color === 'text' ? t.text : s.color },
                    areaStyle: s.area ? { color: s.color === 'text' ? t.text : s.color, opacity: 0.12 } : undefined,
                    z: s.width && s.width > 2 ? 5 : 3,
                    markLine: d.goal && i === 0 ? {
                        symbol: 'none', silent: true,
                        label: { formatter: S.goal + ' ' + eur(d.goal), color: '#b98500', position: 'insideEndTop' },
                        lineStyle: { type: 'dashed', color: '#e0a100', width: 2 },
                        data: [{ yAxis: d.goal }],
                    } : undefined,
                })),
            });
        },

        // Mappa del portafoglio: un riquadro per titolo (area = valore, colore già scelto dall'app), raggruppato per categoria.
        treemap(d, t) {
            const L = d.labels || {};
            const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
            const spct = v => (v >= 0 ? '+' : '−') + pct1.format(Math.abs(v));
            const col = v => (v >= 0 ? t.pos : t.neg);
            const leaves = d.groups.map(g => ({
                name: g.name,
                title: g.name + ' · ' + eur(g.value),
                value: g.value,
                itemStyle: { color: 'transparent', borderColor: t.border },
                children: g.items.map(i => Object.assign({}, i, { itemStyle: { color: i.color } })),
            }));
            return {
                // i riquadri cambiano posto da un giorno all'altro: niente animazione, altrimenti durante la riproduzione si vedono layout sovrapposti
                animation: false,
                textStyle: { fontFamily: 'Instrument Sans, Segoe UI Variable, Segoe UI, sans-serif', color: t.text },
                tooltip: {
                    trigger: 'item', confine: true, backgroundColor: t.card, borderColor: t.border, textStyle: { color: t.text },
                    formatter(p) {
                        const n = p.data;
                        if (n.children) return `<b>${esc(n.name)}</b><br/>${esc(L.value)}: <b>${eur(n.value)}</b>`;
                        let h = `<b>${esc(n.name)}</b><br/>${esc(n.category)}<br/>${esc(L.value)}: <b>${eur(n.value)}</b> (${pct1.format(n.weight)})<br/>`;
                        h += `${esc(L.shares)}: ${plain(n.shares, 4)}`;
                        if (n.price !== null && n.price !== undefined) h += ` · ${esc(L.price)}: ${plain(n.price, 4)}`;
                        h += '<br/>';
                        if (n.pnl !== null && n.pnl !== undefined) {
                            h += `${esc(L.cost)}: ${eur(n.cost)}<br/><span style="color:${col(n.pnl)}">${esc(L.pnl)}: <b>${sign(n.pnl)}</b> (${spct(n.pct)})</span>`;
                        } else {
                            h += `<span style="color:${t.muted}">${esc(L.noCost)}</span>`;
                        }
                        return h;
                    },
                },
                series: [{
                    type: 'treemap', roam: false, nodeClick: false, breadcrumb: { show: false },
                    left: 0, right: 0, top: 0, bottom: 0, squareRatio: 1.1, visibleMin: 60,
                    label: {
                        show: true, color: '#fff', fontSize: 12, fontWeight: 600, overflow: 'truncate', ellipsis: '…',
                        position: 'insideTopLeft', padding: [6, 8],
                        formatter: p => p.data.name + (p.data.pct === null || p.data.pct === undefined ? '' : '\n' + spct(p.data.pct)),
                    },
                    upperLabel: { show: true, height: 24, color: t.text, fontSize: 12, fontWeight: 700, formatter: p => p.data.title || p.name },
                    itemStyle: { borderColor: t.card, borderWidth: 2, gapWidth: 2 },
                    levels: [
                        { itemStyle: { borderColor: t.border, borderWidth: 1, gapWidth: 5 }, upperLabel: { show: true } },
                        { itemStyle: { borderColor: t.card, borderWidth: 2, gapWidth: 2 } },
                    ],
                    data: leaves,
                }],
            };
        },

        // Colonne semplici o impilate: una serie per colore, una colonna per etichetta (costi per mese, incassi dei BTP...).
        bars(d, t) {
            return baseOption(t, d.labels, {
                legend: { top: 4, textStyle: { color: t.text }, icon: 'roundRect', data: d.series.map(s => s.name) },
                tooltip: {
                    trigger: 'axis', axisPointer: { type: 'shadow' }, confine: true,
                    backgroundColor: t.card, borderColor: t.border, textStyle: { color: t.text },
                    valueFormatter: v => eur(v),
                },
                series: d.series.map(s => ({
                    name: s.name, type: 'bar', stack: d.stacked ? 'tot' : undefined, data: s.values,
                    itemStyle: { color: s.color }, barMaxWidth: 46,
                })),
            });
        },

        composition(d, t) {
            const n = d.labels.length;
            const series = d.stacks.map(s => ({
                name: s.name, type: 'bar', stack: 'comp', data: s.values, barGap: '-100%',
                itemStyle: { color: s.color }, emphasis: { focus: 'series' }, z: 2,
            }));
            // Un triangolo sopra ogni colonna: verde in su = sopra i versamenti, rosso in giù = sotto.
            series.push({
                name: S.gap, type: 'scatter', symbol: 'triangle', symbolSize: n > 60 ? 6 : 11, symbolOffset: [0, n > 60 ? -6 : -12],
                data: d.totals.map((v, i) => ({
                    value: Math.max(v, d.stacks.reduce((s, st) => s + Math.max(0, st.values[i]), 0)),
                    symbolRotate: d.gaps[i] >= 0 ? 0 : 180,
                    itemStyle: { color: d.gaps[i] >= 0 ? t.pos : t.neg },
                })),
                z: 7, silent: true, tooltip: { show: false },
            });
            series.push({
                name: S.net, type: 'line', data: d.contributions, showSymbol: n < 40,
                symbolSize: 5, lineStyle: { color: t.text, width: 2, type: 'dashed' }, itemStyle: { color: t.text }, z: 6,
            });
            return baseOption(t, d.labels, {
                legend: { top: 4, textStyle: { color: t.text }, icon: 'roundRect', data: d.stacks.map(s => s.name).concat([S.net]) },
                tooltip: {
                    trigger: 'axis', axisPointer: { type: 'shadow' }, confine: true,
                    backgroundColor: t.card, borderColor: t.border, textStyle: { color: t.text },
                    formatter(params) {
                        const i = params[0].dataIndex;
                        let h = `<b>${d.labels[i]}</b><br/>`;
                        d.stacks.forEach(s => {
                            const v = s.values[i];
                            const prev = i > 0 ? s.values[i - 1] : null;
                            if (!v && !prev) return;
                            let delta = '';
                            if (prev !== null && v !== prev) {
                                const dv = v - prev;
                                delta = ` <span style="color:${dv >= 0 ? t.pos : t.neg}">(${sign(dv)})</span>`;
                            }
                            h += `${dot(s.color)}${s.name}: <b>${eur(v)}</b>${delta}<br/>`;
                        });
                        h += `<hr style="border:0;border-top:1px solid ${t.border};margin:4px 0"/>`;
                        h += `${S.total}: <b>${eur(d.totals[i])}</b><br/>${S.net}: ${eur(d.contributions[i])}<br/>`;
                        const g = d.gaps[i];
                        const gp = d.contributions[i] ? ' (' + pct.format(g / d.contributions[i]) + ')' : '';
                        h += `<span style="color:${g >= 0 ? t.pos : t.neg}">${g >= 0 ? S.above : S.below}: <b>${sign(g)}</b>${gp}</span>`;
                        return h;
                    },
                },
                series,
            });
        },

        gap(d, t) {
            return baseOption(t, d.labels, {
                legend: { top: 4, textStyle: { color: t.text }, icon: 'roundRect', data: [S.base, S.above, S.below, S.net] },
                tooltip: {
                    trigger: 'axis', axisPointer: { type: 'shadow' }, confine: true,
                    backgroundColor: t.card, borderColor: t.border, textStyle: { color: t.text },
                    formatter(params) {
                        const i = params[0].dataIndex;
                        const g = d.gaps[i];
                        const gp = d.contributions[i] ? ' (' + pct.format(g / d.contributions[i]) + ')' : '';
                        return `<b>${d.labels[i]}</b><br/>${S.total}: <b>${eur(d.totals[i])}</b><br/>${S.net}: ${eur(d.contributions[i])}<br/>` +
                            `<span style="color:${g >= 0 ? t.pos : t.neg}">${g >= 0 ? S.above : S.below}: <b>${sign(g)}</b>${gp}</span>`;
                    },
                },
                series: [
                    { name: S.base, type: 'bar', stack: 'g', data: d.base, itemStyle: { color: '#4682B4' }, barMaxWidth: 46 },
                    { name: S.above, type: 'bar', stack: 'g', data: d.capPos, itemStyle: { color: '#2eaa64' } },
                    { name: S.below, type: 'bar', stack: 'g', data: d.capNeg, itemStyle: { color: '#dc3c32' } },
                    {
                        name: S.net, type: 'line', data: d.contributions, showSymbol: true, symbolSize: 5,
                        lineStyle: { color: t.text, width: 2, type: 'dashed' }, itemStyle: { color: t.text }, z: 6,
                    },
                ],
            });
        },
    };

    function draw(entry) {
        const t = theme();
        const option = builders[entry.kind](entry.data, t);
        entry.chart.setOption(option, true);
    }

    function render(el, kind, data, ref) {
        let entry = registry.get(el);
        if (!entry) {
            const chart = echarts.init(el, null, { renderer: 'canvas' });
            const observer = new ResizeObserver(() => chart.resize());
            observer.observe(el);
            entry = { chart, observer };
            registry.set(el, entry);
        }
        entry.kind = kind;
        entry.chart.off('click');
        if (ref) {
            // se l'elemento ha una chiave (i riquadri della mappa) si manda quella, altrimenti il nome
            entry.chart.on('click', p => ref.invokeMethodAsync('ItemClicked', (p.data && p.data.key) || p.name || ''));
        }
        entry.data = data;
        draw(entry);
    }

    function dispose(el) {
        const entry = registry.get(el);
        if (entry) {
            entry.observer.disconnect();
            entry.chart.dispose();
            registry.delete(el);
        }
    }

    function setTheme(name) {
        document.documentElement.setAttribute('data-theme', name);
        // Le variabili CSS cambiano subito: ridisegna i grafici già presenti.
        registry.forEach(entry => { if (entry.data) draw(entry); });
    }

    // Lingua: formati dei numeri (it-IT, en-IE, fr-FR…) e le poche parole fisse dei grafici, già tradotte dall'app.
    function setLocale(name, strings) {
        locale = name || 'it-IT';
        Object.assign(S, strings || {});
        buildFormats();
        registry.forEach(entry => { if (entry.data) draw(entry); });
    }

    return { render, dispose, setTheme, setLocale };
})();
