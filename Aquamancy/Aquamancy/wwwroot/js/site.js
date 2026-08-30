// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', function () {
    var config = window.aquamancyConfig || {};

    // Clock setup
    function updateClock() {
        var el = document.getElementById('time');
        if (!el) return;
        var now = new Date();
        var hh = String(now.getHours()).padStart(2, '0');
        var mm = String(now.getMinutes()).padStart(2, '0');
        el.textContent = hh + ':' + mm;
    }
    updateClock();
    setInterval(updateClock, 1000);

    // Ajuste la taille de police du nom de sonde pour qu'il tienne dans le header
    function fitProbeName(nameEl) {
        var header = nameEl.parentElement;
        if (!header) return;
        var maxFontSize = 2; // rem
        var minFontSize = 0.6; // rem
        var step = 0.05;
        var size = maxFontSize;
        nameEl.style.fontSize = size + 'rem';
        var available = header.clientWidth
            - parseFloat(getComputedStyle(header).paddingLeft)
            - parseFloat(getComputedStyle(header).paddingRight);
        while (nameEl.scrollWidth > available && size > minFontSize) {
            size -= step;
            nameEl.style.fontSize = size + 'rem';
        }
    }

    function fitAllProbeNames() {
        document.querySelectorAll('.probe-name').forEach(fitProbeName);
    }

    fitAllProbeNames();
    window.addEventListener('resize', fitAllProbeNames);

    // Modale des paramètres de l'application : met en pause le rafraîchissement automatique tant qu'elle est ouverte
    (function () {
        var appModalEl = document.getElementById('appSettingsModal');
        if (!appModalEl) return;
        window.appSettingsModalOpen = false;
        appModalEl.addEventListener('shown.bs.modal', function () { window.appSettingsModalOpen = true; });
        appModalEl.addEventListener('hidden.bs.modal', function () { window.appSettingsModalOpen = false; });
    })();

    // Probe settings modal
    (function () {
        var modalEl = document.getElementById('probeSettingsModal');
        if (!modalEl || typeof bootstrap === 'undefined') return;

        var modal = new bootstrap.Modal(modalEl);
        var form = document.getElementById('probeSettingsForm');
        var errorEl = document.getElementById('probeSettingsError');
        var statusEl = document.getElementById('probeStatusInfo');

        // Met en pause le rafraîchissement automatique tant que la modale est ouverte
        window.probeModalOpen = false;
        modalEl.addEventListener('shown.bs.modal', function () { window.probeModalOpen = true; });
        modalEl.addEventListener('hidden.bs.modal', function () { window.probeModalOpen = false; });

        function setValue(id, value) {
            var el = document.getElementById(id);
            if (el) el.value = value == null ? '' : value;
        }

        // Synchronisation des sliders avec les champs numériques
        function syncSlidersFromNumbers() {
            document.querySelectorAll('#probeSettingsModal .probe-slider').forEach(function (slider) {
                var number = document.getElementById(slider.getAttribute('data-number'));
                if (number && number.value !== '') {
                    slider.value = number.value;
                }
            });
        }

        document.querySelectorAll('#probeSettingsModal .probe-slider').forEach(function (slider) {
            var number = document.getElementById(slider.getAttribute('data-number'));
            if (!number) return;
            slider.addEventListener('input', function () { number.value = slider.value; });
            number.addEventListener('input', function () { slider.value = number.value; });
        });

        // Force des entiers pour les champs de température (min/max)
        ['probe-MinTemperature', 'probe-MaxTemperature'].forEach(function (id) {
            var number = document.getElementById(id);
            if (!number) return;
            number.addEventListener('change', function () {
                if (number.value === '') return;
                var rounded = Math.round(parseFloat(number.value));
                if (!isNaN(rounded)) {
                    number.value = rounded;
                    var slider = document.querySelector('#probeSettingsModal .probe-slider[data-number="' + id + '"]');
                    if (slider) slider.value = rounded;
                }
            });
        });

        function openForProbe(probeId, statusMessage) {
            errorEl.classList.add('d-none');
            errorEl.textContent = '';
            if (statusEl) {
                if (statusMessage) {
                    statusEl.textContent = statusMessage;
                    statusEl.classList.remove('d-none');
                } else {
                    statusEl.textContent = '';
                    statusEl.classList.add('d-none');
                }
            }
            fetch('?handler=Probe&id=' + encodeURIComponent(probeId), {
                headers: { 'Accept': 'application/json' }
            })
                .then(function (r) {
                    if (!r.ok) throw new Error('Impossible de récupérer la sonde.');
                    return r.json();
                })
                .then(function (p) {
                    setValue('probe-Id', p.id);
                    setValue('probe-Name', p.name);
                    setValue('probe-MachineName', p.machineName);
                    setValue('probe-Color', p.color || '#007bff');
                    setValue('probe-MinTemperature', p.minTemperature);
                    setValue('probe-MaxTemperature', p.maxTemperature);
                    setValue('probe-SendFrequencyInSeconds', p.sendFrequencyInSeconds);
                    var tdsEnabledEl = document.getElementById('probe-TdsEnabled');
                    if (tdsEnabledEl) tdsEnabledEl.checked = !!p.tdsEnabled;
                    syncSlidersFromNumbers();
                    modal.show();
                })
                .catch(function (e) {
                    errorEl.textContent = e.message;
                    errorEl.classList.remove('d-none');
                    modal.show();
                });
        }

        document.querySelectorAll('.probe-card').forEach(function (card) {
            function handler() { openForProbe(card.getAttribute('data-probe-id'), card.getAttribute('data-status')); }
            card.addEventListener('click', handler);
            card.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handler(); }
            });
        });

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            errorEl.classList.add('d-none');
            var formData = new FormData(form);
            var token = form.querySelector('input[name="__RequestVerificationToken"]');
            fetch('?handler=UpdateProbe', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token ? token.value : '' },
                body: formData
            })
                .then(function (r) {
                    if (!r.ok) throw new Error('Échec de la mise à jour.');
                    return r.json();
                })
                .then(function () {
                    modal.hide();
                    window.location.reload();
                })
                .catch(function (err) {
                    errorEl.textContent = err.message;
                    errorEl.classList.remove('d-none');
                });
        });
    })();

    // Generic chart creation function
    function createChart(canvasId, data, yAxisTitle) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) return null;
        var ctx = canvas.getContext('2d');

        const baseFontSize = (typeof Chart !== 'undefined' && Chart.defaults && Chart.defaults.font && Chart.defaults.font.size) ? Chart.defaults.font.size : 12;
        const doubleSize = baseFontSize * config.fontSizeMultiplier;

        const chartConfig = {
            type: 'line',
            data: data,
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                transitions: {},
                font: { size: doubleSize },
                plugins: {
                    title: {
                        text: yAxisTitle,
                        display: true,
                        font: { size: doubleSize }
                    },
                    legend: {
                        display: false,
                        labels: {
                            font: { size: doubleSize }
                        }
                    },
                    tooltip: {
                        titleFont: { size: doubleSize },
                        bodyFont: { size: doubleSize }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            color: '#e6e6e6'
                        },
                        type: 'time',
                        time: {
                            tooltipFormat: 'dd/MM HH:mm',
                            displayFormats: {
                                hour: "HH'h'",
                                day: "dd/MM",
                                week: "dd/MM",
                                month: "MM/yyyy"
                            }
                        },
                        title: {
                            display: false,
                            text: 'Date',
                            font: { size: doubleSize }
                        },
                        ticks: {
                            font: { size: doubleSize }
                        }
                    },
                    y: {
                        grace: '0.5',
                        grid: {
                            color: '#e6e6e6'
                        },
                        title: {
                            display: false,
                            text: yAxisTitle,
                            font: { size: doubleSize }
                        },
                        ticks: {
                            font: { size: doubleSize },
                            stepSize: 1,
                            precision: 0
                        }
                    }
                }
            }
        };

        return new Chart(ctx, chartConfig);
    }

    // Create charts
    var temperatureData = config.temperatureChart;
    var tdsData = config.tdsChart;

    // Génère une image à partir d'un emoji/glyphe, utilisable comme pointStyle Chart.js.
    // 'color' force la couleur du glyphe (ignoré par les emojis couleur comme 🔥).
    function makeEmojiImage(emoji, size, color) {
        var canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        var ctx = canvas.getContext('2d');
        ctx.font = (size - 2) + 'px serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        if (color) ctx.fillStyle = color;
        ctx.fillText(emoji, size / 2, size / 2);
        var img = new Image(size, size);
        img.src = canvas.toDataURL();
        return img;
    }

    // Remplace les jetons de forme "emoji" par de vraies images dans chaque dataset
    function applyEmojiPointStyles(chartData) {
        if (!chartData) return;
        var flameImg = makeEmojiImage('🔥', 22);
        // Flocon texte '❄' coloré en bleu (couleur fiable, indépendante de la police emoji)
        var snowImg = makeEmojiImage('❄', 22, config.coldColor);
        (chartData.datasets || []).forEach(function (dataset) {
            if (!Array.isArray(dataset.pointStyle)) return;
            dataset.pointStyle = dataset.pointStyle.map(function (style) {
                if (style === 'flame') return flameImg;
                if (style === 'snow') return snowImg;
                return style;
            });
        });
    }

    applyEmojiPointStyles(temperatureData);
    applyEmojiPointStyles(tdsData);

    createChart('temperatureChart', temperatureData, 'Température');
    createChart('tdsChart', tdsData, 'TDS');

    // Refresh the page every x - TODO avoid full reload and use ajax ?
    var refreshInterval = config.pageRefreshIntervalInMilliseconds;
    var refreshStart = Date.now();
    var progressBar = document.getElementById('refreshProgressBar');
    var lastElapsed = 0;

    function updateRefreshProgress() {
        if (!progressBar) return;
        // Pause le compteur tant que la modale de paramétrage est ouverte
        if (window.probeModalOpen || window.appSettingsModalOpen) {
            refreshStart = Date.now() - lastElapsed;
            return;
        }
        var elapsed = Date.now() - refreshStart;
        lastElapsed = elapsed;
        var percent = Math.min(100, (elapsed / refreshInterval) * 100);
        progressBar.style.width = percent + '%';
        progressBar.setAttribute('aria-valuenow', Math.round(percent));
        if (elapsed >= refreshInterval) {
            location.reload();
        }
    }
    updateRefreshProgress();
    setInterval(updateRefreshProgress, 200);
});
