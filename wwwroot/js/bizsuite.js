

'use strict';

$(function () {
    BizSuite.init();
});

const BizSuite = {
    
    init() {
        this.bindSidebar();
        this.bindDropdowns();
        this.bindTooltips();
        this.initActiveNav();
        this.animateKPICounters();
        this.bindTableSearch();
        this.bindFilterSelects();
    },

    bindSidebar() {
        const $sidebar  = $('.sidebar');
        const $overlay  = $('.sidebar-overlay');
        const $menuBtn  = $('.mobile-menu-btn');

        $menuBtn.on('click', function () {
            $sidebar.toggleClass('open');
            $overlay.toggleClass('show');
        });

        $overlay.on('click', function () {
            $sidebar.removeClass('open');
            $overlay.removeClass('show');
        });

        $('.sidebar-nav-item').on('click', function () {
            if ($(window).width() <= 1024) {
                $sidebar.removeClass('open');
                $overlay.removeClass('show');
            }
        });
    },

    bindDropdowns() {
        
        $(document).on('click', '[data-dropdown]', function (e) {
            e.stopPropagation();
            const target = $(this).data('dropdown');
            $('#' + target).toggleClass('show');
        });

        $(document).on('click', function () {
            $('.dropdown-biz').removeClass('show');
        });

        $('.dropdown-biz').on('click', function (e) {
            e.stopPropagation();
        });
    },

    initActiveNav() {
        const path = window.location.pathname.toLowerCase();
        $('.sidebar-nav-item[href]').each(function () {
            const href = $(this).attr('href').toLowerCase();
            if (path === href || (href !== '/' && path.startsWith(href))) {
                $(this).addClass('active');
            }
        });
    },

    animateKPICounters() {
        $('.kpi-value[data-target]').each(function () {
            const $el   = $(this);
            const raw   = $el.data('target');
            const isNum = !isNaN(raw.toString().replace(/[₹,]/g, ''));

            if (!isNum) return;

            const prefix  = raw.toString().match(/^[₹$£]/) ? raw.toString()[0] : '';
            const numeric = parseFloat(raw.toString().replace(/[₹$£,]/g, ''));
            const suffix  = raw.toString().match(/[KMB]$/) ? raw.toString().slice(-1) : '';
            const duration = 1200;
            const steps   = 50;
            const increment = numeric / steps;
            let current = 0;
            const suffix_     = suffix;

            const timer = setInterval(function () {
                current += increment;
                if (current >= numeric) {
                    current = numeric;
                    clearInterval(timer);
                }
                const formatted = BizSuite.formatNumber(Math.floor(current));
                $el.text(prefix + formatted + suffix_);
            }, duration / steps);
        });
    },

    formatNumber(n) {
        return n.toLocaleString('en-IN');
    },

    bindTableSearch() {
        $(document).on('input', '.table-search-input', function () {
            const q   = $(this).val().toLowerCase();
            const $tb = $($(this).data('target') || '.table-biz');

            $tb.find('tbody tr').each(function () {
                const text = $(this).text().toLowerCase();
                $(this).toggle(text.includes(q));
            });
        });
    },

    bindFilterSelects() {
        $(document).on('change', '.filter-select[data-col]', function () {
            const col = parseInt($(this).data('col')) - 1;
            const val = $(this).val().toLowerCase();
            const $tb = $($(this).data('target') || '.table-biz');

            $tb.find('tbody tr').each(function () {
                const cell = $(this).find('td').eq(col).text().toLowerCase();
                $(this).toggle(val === '' || cell.includes(val));
            });
        });
    },

    bindTooltips() {
        
    },

    showToast(msg, type = 'success') {
        const icons = {
            success: 'fa-circle-check',
            error:   'fa-circle-xmark',
            warning: 'fa-triangle-exclamation',
        };
        const $toast = $(`
            <div class="toast-biz">
                <i class="fa-solid ${icons[type]} toast-icon ${type}"></i>
                <span class="toast-msg">${msg}</span>
            </div>
        `);
        $('#toast-container').append($toast);
        setTimeout(() => {
            $toast.css({ opacity: 0, transform: 'translateY(16px)', transition: 'all 0.3s ease' });
            setTimeout(() => $toast.remove(), 350);
        }, 3200);
    },

    initRevenueChart(canvasId, labels, data) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        return new Chart(ctx, {
            type: 'bar',
            data: {
                labels,
                datasets: [{
                    label: 'Revenue (₹)',
                    data,
                    backgroundColor: labels.map((_, i) =>
                        i === data.indexOf(Math.max(...data))
                            ? 'rgba(201,247,111,0.85)'
                            : 'rgba(126,227,196,0.35)'
                    ),
                    borderColor: labels.map((_, i) =>
                        i === data.indexOf(Math.max(...data))
                            ? '#C9F76F'
                            : 'rgba(126,227,196,0.5)'
                    ),
                    borderWidth: 2,
                    borderRadius: 8,
                    borderSkipped: false,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: 'rgba(20,61,52,0.95)',
                        borderColor: 'rgba(126,227,196,0.2)',
                        borderWidth: 1,
                        titleColor: '#A0B3AD',
                        bodyColor: '#C9F76F',
                        bodyFont: { size: 14, weight: 'bold' },
                        padding: 12,
                        cornerRadius: 10,
                        callbacks: {
                            label: (ctx) => '₹' + ctx.parsed.y.toLocaleString('en-IN')
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { color: '#6B7D77', font: { size: 12 } },
                        border: { display: false }
                    },
                    y: {
                        grid: { color: 'rgba(255,255,255,0.04)', drawBorder: false },
                        ticks: {
                            color: '#6B7D77', font: { size: 12 },
                            callback: v => '₹' + (v >= 1000 ? (v / 1000) + 'K' : v)
                        },
                        border: { display: false }
                    }
                }
            }
        });
    },

    initTrendChart(canvasId, labels, data) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        return new Chart(ctx, {
            type: 'line',
            data: {
                labels,
                datasets: [{
                    label: 'Sales',
                    data,
                    borderColor: '#C9F76F',
                    backgroundColor: 'rgba(201,247,111,0.07)',
                    borderWidth: 2.5,
                    tension: 0.45,
                    pointBackgroundColor: '#C9F76F',
                    pointRadius: 4,
                    pointHoverRadius: 7,
                    fill: true,
                }]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: 'rgba(20,61,52,0.95)',
                        borderColor: 'rgba(126,227,196,0.2)',
                        borderWidth: 1,
                        titleColor: '#A0B3AD',
                        bodyColor: '#C9F76F',
                        padding: 12,
                        cornerRadius: 10,
                        callbacks: { label: (c) => '₹' + c.parsed.y.toLocaleString('en-IN') }
                    }
                },
                scales: {
                    x: { grid: { display: false }, ticks: { color: '#6B7D77', font: { size: 12 } }, border: { display: false } },
                    y: {
                        grid: { color: 'rgba(255,255,255,0.04)' },
                        ticks: { color: '#6B7D77', font: { size: 12 }, callback: v => '₹' + (v >= 1000 ? (v / 1000) + 'K' : v) },
                        border: { display: false }
                    }
                }
            }
        });
    },

    initTopProductsChart(canvasId, labels, data) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        return new Chart(ctx, {
            type: 'bar',
            data: {
                labels,
                datasets: [{
                    label: 'Units Sold',
                    data,
                    backgroundColor: 'rgba(126,227,196,0.35)',
                    borderColor: '#7EE3C4',
                    borderWidth: 2,
                    borderRadius: 6,
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true, maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: 'rgba(20,61,52,0.95)',
                        borderColor: 'rgba(126,227,196,0.2)',
                        borderWidth: 1,
                        padding: 12, cornerRadius: 10,
                        titleColor: '#A0B3AD', bodyColor: '#7EE3C4',
                    }
                },
                scales: {
                    x: { grid: { color: 'rgba(255,255,255,0.04)' }, ticks: { color: '#6B7D77', font: { size: 11 } }, border: { display: false } },
                    y: { grid: { display: false }, ticks: { color: '#A0B3AD', font: { size: 12 } }, border: { display: false } }
                }
            }
        });
    },

    initCategoryChart(canvasId, labels, data) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        return new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels,
                datasets: [{
                    data,
                    backgroundColor: ['#C9F76F', '#7EE3C4', '#258CFB', '#F1F5F2', '#A0B3AD'],
                    borderWidth: 0,
                    hoverOffset: 15
                }]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                cutout: '72%',
                plugins: {
                    legend: { position: 'bottom', labels: { color: '#A0B3AD', padding: 20, font: { size: 12 } } },
                    tooltip: {
                        backgroundColor: 'rgba(20,61,52,0.95)', padding: 12, cornerRadius: 10,
                        callbacks: { label: (c) => ' ' + c.label + ': ₹' + c.parsed.toLocaleString('en-IN') }
                    }
                }
            }
        });
    },

    post(url, data, onSuccess, onError) {
        $.ajax({
            url, type: 'POST',
            data: JSON.stringify(data),
            contentType: 'application/json',
            headers: { 'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() },
            success: onSuccess,
            error: onError || function (xhr) {
                BizSuite.showToast('Request failed: ' + (xhr.responseJSON?.message || 'Unknown error'), 'error');
            }
        });
    },

    addOrderLine() {
        const $container = $('#order-lines');
        const idx = $container.children().length;
        const tpl = `
            <div class="order-line d-flex align-center gap-12 mb-16">
                <div style="flex:2">
                    <select name="Items[${idx}].ProductId" class="form-biz-control product-select" required>
                        <option value="">Select Product</option>
                    </select>
                </div>
                <div style="flex:1">
                    <input type="number" name="Items[${idx}].Quantity" class="form-biz-control" placeholder="Qty" min="1" required />
                </div>
                <div style="flex:1">
                    <input type="number" name="Items[${idx}].UnitPrice" class="form-biz-control" placeholder="Unit Price" step="0.01" readonly />
                </div>
                <div style="flex:1">
                    <input type="number" name="Items[${idx}].LineTotal" class="form-biz-control" placeholder="Total" readonly />
                </div>
                <button type="button" class="btn-biz-danger remove-line" style="flex-shrink:0">
                    <i class="fa-solid fa-trash-can"></i>
                </button>
            </div>
        `;
        $container.append(tpl);
        BizSuite.loadProductOptions($container.find('.product-select').last());
    },

    removeOrderLine($btn) {
        $btn.closest('.order-line').remove();
        BizSuite.recalcTotal();
    },

    recalcTotal() {
        let total = 0;
        $('.order-line').each(function () {
            const qty   = parseFloat($(this).find('[name*="Quantity"]').val()) || 0;
            const price = parseFloat($(this).find('[name*="UnitPrice"]').val()) || 0;
            const line  = qty * price;
            $(this).find('[name*="LineTotal"]').val(line.toFixed(2));
            total += line;
        });
        $('#order-total').text('₹' + total.toLocaleString('en-IN'));
    },

    loadProductOptions($select) {
        $.get('/Staff/GetProducts', function (data) {
            data.forEach(p => {
                const stockLabel = p.stockQuantity <= 0 ? " — (Out of Stock)" : "";
                $select.append(`<option value="${p.productId}" data-price="${p.price}" data-stock="${p.stockQuantity}">${p.productName}${stockLabel}</option>`);
            });
        });
    }
};

$(document).on('change', '.product-select', function () {
    const $selected = $(this).find('option:selected');
    const price = $selected.data('price') || 0;
    const stock = parseInt($selected.data('stock'));

    const $line = $(this).closest('.order-line');

    if ($(this).val() !== "" && stock <= 0) {
        BizSuite.showToast('Insufficient Inventory: This product is completely out of stock.', 'warning');
        $(this).val(''); 
        $line.find('[name*="UnitPrice"]').val('');
        BizSuite.recalcTotal();
        return;
    }

    $line.find('[name*="UnitPrice"]').val(price);
    BizSuite.recalcTotal();
});

$(document).on('input', '.order-line [name*="Quantity"]', function () {
    BizSuite.recalcTotal();
});

$(document).on('click', '#add-line-btn', function () {
    BizSuite.addOrderLine();
});

$(document).on('click', '.remove-line', function () {
    BizSuite.removeOrderLine($(this));
});
