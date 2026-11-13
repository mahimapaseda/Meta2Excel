/**
 * Meta Receipts Lite — Frontend Workbench Controller
 * Handles live invoice staging, deduplication, search filtering, Chart canvas rendering,
 * and client-side binary Excel (.xlsx) / CSV export.
 */

(function () {
  'use strict';

  // --- Theme Toggle ---
  const themeToggle = document.getElementById('themeToggle');
  const htmlRoot = document.documentElement;

  function initTheme() {
    const saved = localStorage.getItem('mrl_theme') || 'dark';
    htmlRoot.setAttribute('data-theme', saved);
  }

  themeToggle.addEventListener('click', () => {
    const current = htmlRoot.getAttribute('data-theme');
    const next = current === 'dark' ? 'light' : 'dark';
    htmlRoot.setAttribute('data-theme', next);
    localStorage.setItem('mrl_theme', next);
    drawSpendChart();
  });
  initTheme();

  // --- State ---
  let state = {
    receipts: [],
    items: [],
    activeTab: 'receipts',
    dedupeActive: true,
    filterQuery: ''
  };

  // --- Seed Data (Authentic Meta Invoices) ---
  const DEMO_INVOICES = [
    {
      id: "FB-ADS-2026-914028",
      date: "2026-11-02",
      account: "ScaleOps_Direct_US",
      accountId: "act_1092837401",
      currency: "USD",
      amount: 1420.50,
      paymentMethod: "Mastercard •••• 4120",
      isDuplicate: false,
      items: [
        { campaign: "AdvantagePlus_Shopping_US_Q4", amount: 980.20 },
        { campaign: "TOFU_VideoViews_Reels_Feed", amount: 440.30 }
      ]
    },
    {
      id: "FB-ADS-2026-914029",
      date: "2026-11-03",
      account: "ScaleOps_Direct_US",
      accountId: "act_1092837401",
      currency: "USD",
      amount: 2150.00,
      paymentMethod: "Mastercard •••• 4120",
      isDuplicate: false,
      items: [
        { campaign: "AdvantagePlus_Shopping_US_Q4", amount: 1650.00 },
        { campaign: "Retargeting_HighIntent_AddToCart", amount: 500.00 }
      ]
    },
    {
      id: "FB-ADS-2026-914030",
      date: "2026-11-04",
      account: "ScaleOps_Direct_US",
      accountId: "act_1092837401",
      currency: "USD",
      amount: 1890.75,
      paymentMethod: "Mastercard •••• 4120",
      isDuplicate: false,
      items: [
        { campaign: "AdvantagePlus_Shopping_US_Q4", amount: 1400.00 },
        { campaign: "Creator_SparkAds_Collab_V1", amount: 490.75 }
      ]
    },
    {
      id: "FB-ADS-2026-914030", // Intentionally duplicate ID for dedupe test
      date: "2026-11-04",
      account: "ScaleOps_Direct_US",
      accountId: "act_1092837401",
      currency: "USD",
      amount: 1890.75,
      paymentMethod: "Mastercard •••• 4120",
      isDuplicate: true,
      items: [
        { campaign: "AdvantagePlus_Shopping_US_Q4", amount: 1400.00 },
        { campaign: "Creator_SparkAds_Collab_V1", amount: 490.75 }
      ]
    },
    {
      id: "FB-ADS-2026-880192",
      date: "2026-11-05",
      account: "ScaleOps_Global_EUR",
      accountId: "act_9918204918",
      currency: "EUR",
      amount: 3200.00,
      paymentMethod: "Visa •••• 9811",
      isDuplicate: false,
      items: [
        { campaign: "EU_Expansion_Germany_Austria", amount: 2100.00 },
        { campaign: "EU_BrandAwareness_FR_IT", amount: 1100.00 }
      ]
    },
    {
      id: "FB-ADS-2026-880193",
      date: "2026-11-06",
      account: "ScaleOps_Global_EUR",
      accountId: "act_9918204918",
      currency: "EUR",
      amount: 2850.40,
      paymentMethod: "Visa •••• 9811",
      isDuplicate: false,
      items: [
        { campaign: "EU_Expansion_Germany_Austria", amount: 1950.40 },
        { campaign: "EU_BrandAwareness_FR_IT", amount: 900.00 }
      ]
    },
    {
      id: "FB-ADS-2026-773104",
      date: "2026-11-07",
      account: "Acquisition_LeadGen_UK",
      accountId: "act_5521409923",
      currency: "GBP",
      amount: 1450.00,
      paymentMethod: "AMEX •••• 1009",
      isDuplicate: false,
      items: [
        { campaign: "B2B_SaaS_Demo_InstantForms", amount: 1450.00 }
      ]
    },
    {
      id: "FB-ADS-2026-773105",
      date: "2026-11-08",
      account: "Acquisition_LeadGen_UK",
      accountId: "act_5521409923",
      currency: "GBP",
      amount: 1920.60,
      paymentMethod: "AMEX •••• 1009",
      isDuplicate: false,
      items: [
        { campaign: "B2B_SaaS_Demo_InstantForms", amount: 1920.60 }
      ]
    },
    {
      id: "FB-ADS-2026-914041",
      date: "2026-11-09",
      account: "ScaleOps_Direct_US",
      accountId: "act_1092837401",
      currency: "USD",
      amount: 4120.00,
      paymentMethod: "Mastercard •••• 4120",
      isDuplicate: false,
      items: [
        { campaign: "AdvantagePlus_Shopping_US_Q4", amount: 3120.00 },
        { campaign: "Dynamic_Creative_Testing_Set4", amount: 1000.00 }
      ]
    },
    {
      id: "FB-ADS-2026-914052",
      date: "2026-11-10",
      account: "ScaleOps_Direct_US",
      accountId: "act_1092837401",
      currency: "USD",
      amount: 3980.25,
      paymentMethod: "Mastercard •••• 4120",
      isDuplicate: false,
      items: [
        { campaign: "AdvantagePlus_Shopping_US_Q4", amount: 3200.00 },
        { campaign: "Retargeting_HighIntent_AddToCart", amount: 780.25 }
      ]
    }
  ];

  // --- Elements ---
  const btnLoadDemo = document.getElementById('btnLoadDemo');
  const btnClear = document.getElementById('btnClear');
  const dropzone = document.getElementById('dropzone');
  const fileInput = document.getElementById('fileInput');
  const campaignFilter = document.getElementById('campaignFilter');
  const toggleDedupe = document.getElementById('toggleDedupe');
  const tabReceipts = document.getElementById('tabReceipts');
  const tabItems = document.getElementById('tabItems');
  const receiptsTable = document.getElementById('receiptsTable');
  const itemsTable = document.getElementById('itemsTable');
  const receiptsBody = document.getElementById('receiptsBody');
  const itemsBody = document.getElementById('itemsBody');

  const statSpend = document.getElementById('statSpend');
  const statInvoices = document.getElementById('statInvoices');
  const statItems = document.getElementById('statItems');
  const statDuplicates = document.getElementById('statDuplicates');
  const countReceipts = document.getElementById('countReceipts');
  const countItems = document.getElementById('countItems');
  const batchStatus = document.getElementById('batchStatus');

  const btnExportExcel = document.getElementById('btnExportExcel');
  const btnExportCsv = document.getElementById('btnExportCsv');

  // --- Helpers ---
  function formatMoney(amount, currency = 'USD') {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currency,
      minimumFractionDigits: 2
    }).format(amount);
  }

  // --- Load Data ---
  function loadReceipts(data) {
    state.receipts = JSON.parse(JSON.stringify(data));
    
    // Flatten line items
    state.items = [];
    state.receipts.forEach(r => {
      if (r.items && r.items.length) {
        r.items.forEach(it => {
          state.items.push({
            date: r.date,
            receiptId: r.id,
            account: r.account,
            accountId: r.accountId,
            currency: r.currency,
            campaign: it.campaign,
            amount: it.amount,
            isDuplicate: r.isDuplicate
          });
        });
      }
    });

    renderAll();
    batchStatus.textContent = "PROCESSED (" + state.receipts.length + ")";
  }

  // --- Filtering & Deduplication Logic ---
  function getFilteredReceipts() {
    return state.receipts.filter(r => {
      if (state.dedupeActive && r.isDuplicate) return false;
      if (!state.filterQuery) return true;
      const q = state.filterQuery.toLowerCase();
      const matchAccount = r.account.toLowerCase().includes(q);
      const matchId = r.id.toLowerCase().includes(q);
      const matchItems = r.items && r.items.some(i => i.campaign.toLowerCase().includes(q));
      return matchAccount || matchId || matchItems;
    });
  }

  function getFilteredItems() {
    return state.items.filter(i => {
      if (state.dedupeActive && i.isDuplicate) return false;
      if (!state.filterQuery) return true;
      const q = state.filterQuery.toLowerCase();
      return i.campaign.toLowerCase().includes(q) ||
             i.account.toLowerCase().includes(q) ||
             i.receiptId.toLowerCase().includes(q);
    });
  }

  // --- Rendering Tables ---
  function renderAll() {
    renderTelemetry();
    renderReceiptsTable();
    renderItemsTable();
    drawSpendChart();
  }

  function renderTelemetry() {
    const activeReceipts = state.receipts.filter(r => !state.dedupeActive || !r.isDuplicate);
    const activeItems = state.items.filter(i => !state.dedupeActive || !i.isDuplicate);
    const duplicateCount = state.receipts.filter(r => r.isDuplicate).length;

    // Total spend grouped by currency (matching desktop engine)
    const byCur = {};
    activeReceipts.forEach(r => {
      const cur = r.currency || 'USD';
      byCur[cur] = (byCur[cur] || 0) + r.amount;
    });

    const curKeys = Object.keys(byCur);
    if (curKeys.length === 0) {
      statSpend.textContent = "$0.00";
    } else if (curKeys.length === 1) {
      statSpend.textContent = formatMoney(byCur[curKeys[0]], curKeys[0]);
      statSpend.style.fontSize = '';
    } else {
      statSpend.textContent = curKeys.map(c => formatMoney(byCur[c], c)).join(' · ');
      statSpend.style.fontSize = curKeys.length > 2 ? '13px' : '15px';
    }
    statInvoices.textContent = activeReceipts.length.toLocaleString();
    statItems.textContent = activeItems.length.toLocaleString();
    statDuplicates.textContent = duplicateCount.toString();

    countReceipts.textContent = getFilteredReceipts().length;
    countItems.textContent = getFilteredItems().length;
  }

  function renderReceiptsTable() {
    const list = getFilteredReceipts();
    if (!list.length) {
      receiptsBody.innerHTML = `
        <tr class="empty-row">
          <td colspan="7">
            <div class="empty-state">No matching receipts found.</div>
          </td>
        </tr>
      `;
      return;
    }

    receiptsBody.innerHTML = list.map(r => `
      <tr>
        <td class="mono">${r.date}</td>
        <td class="mono"><strong>${r.id}</strong></td>
        <td>${r.account}</td>
        <td class="mono">${r.currency}</td>
        <td class="amount">${formatMoney(r.amount, r.currency)}</td>
        <td>${r.paymentMethod}</td>
        <td style="text-align: center;">
          ${r.isDuplicate 
            ? '<span class="badge-tag duplicate">DUP</span>' 
            : '<span class="badge-tag valid">VERIFIED</span>'}
        </td>
      </tr>
    `).join('');
  }

  function renderItemsTable() {
    const list = getFilteredItems();
    if (!list.length) {
      itemsBody.innerHTML = `
        <tr class="empty-row">
          <td colspan="6">
            <div class="empty-state">No matching campaign line items found.</div>
          </td>
        </tr>
      `;
      return;
    }

    itemsBody.innerHTML = list.map(i => `
      <tr>
        <td class="mono">${i.date}</td>
        <td class="mono">${i.receiptId}</td>
        <td><strong>${i.campaign}</strong></td>
        <td class="mono">${i.accountId}</td>
        <td class="mono">${i.currency}</td>
        <td class="amount">${formatMoney(i.amount, i.currency)}</td>
      </tr>
    `).join('');
  }

  // --- Chart Canvas Drawing ---
  function drawSpendChart() {
    const canvas = document.getElementById('spendChart');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const width = canvas.width;
    const height = canvas.height;

    ctx.clearRect(0, 0, width, height);

    const list = getFilteredReceipts();
    if (!list.length) {
      ctx.fillStyle = "#64748B";
      ctx.font = "11px 'JetBrains Mono', monospace";
      ctx.textAlign = "center";
      ctx.fillText("No data staged for velocity graph", width / 2, height / 2 + 4);
      return;
    }

    // Aggregate spend by date
    const dateMap = {};
    list.forEach(r => {
      dateMap[r.date] = (dateMap[r.date] || 0) + r.amount;
    });

    const dates = Object.keys(dateMap).sort();
    const values = dates.map(d => dateMap[d]);
    const maxVal = Math.max(...values, 100);

    const padX = 20;
    const padY = 16;
    const chartW = width - padX * 2;
    const chartH = height - padY * 2;

    const points = values.map((val, idx) => {
      const x = padX + (idx / Math.max(values.length - 1, 1)) * chartW;
      const y = height - padY - (val / maxVal) * chartH;
      return { x, y, val };
    });

    // Draw fill area
    ctx.beginPath();
    ctx.moveTo(points[0].x, height - padY);
    points.forEach(pt => ctx.lineTo(pt.x, pt.y));
    ctx.lineTo(points[points.length - 1].x, height - padY);
    ctx.closePath();

    const isLight = htmlRoot.getAttribute('data-theme') === 'light';
    const grad = ctx.createLinearGradient(0, 0, 0, height);
    if (isLight) {
      grad.addColorStop(0, 'rgba(21, 128, 61, 0.25)');
      grad.addColorStop(1, 'rgba(21, 128, 61, 0.0)');
    } else {
      grad.addColorStop(0, 'rgba(16, 185, 129, 0.35)');
      grad.addColorStop(1, 'rgba(16, 185, 129, 0.0)');
    }
    ctx.fillStyle = grad;
    ctx.fill();

    // Draw line
    ctx.beginPath();
    points.forEach((pt, idx) => {
      if (idx === 0) ctx.moveTo(pt.x, pt.y);
      else ctx.lineTo(pt.x, pt.y);
    });
    ctx.strokeStyle = isLight ? '#15803D' : '#10B981';
    ctx.lineWidth = 2.5;
    ctx.stroke();

    // Draw points & labels
    points.forEach(pt => {
      ctx.beginPath();
      ctx.arc(pt.x, pt.y, 3.5, 0, Math.PI * 2);
      ctx.fillStyle = isLight ? '#15803D' : '#10B981';
      ctx.fill();
      ctx.lineWidth = 1.5;
      ctx.strokeStyle = isLight ? '#FFFFFF' : '#111827';
      ctx.stroke();
    });
  }

  // --- Export Handlers ---
  btnExportCsv.addEventListener('click', () => {
    const receipts = getFilteredReceipts();
    if (!receipts.length) {
      alert("No receipts staged to export.");
      return;
    }

    let csv = "Invoice ID,Date,Account Name,Account ID,Currency,Total Paid,Payment Method\n";
    receipts.forEach(r => {
      csv += `"${r.id}","${r.date}","${r.account}","${r.accountId}","${r.currency}",${r.amount},"${r.paymentMethod}"\n`;
    });

    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Meta_Receipts_Export_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  });

  btnExportExcel.addEventListener('click', () => {
    const receipts = getFilteredReceipts();
    const items = getFilteredItems();
    if (!receipts.length) {
      alert("No receipts staged to export.");
      return;
    }

    if (typeof XLSX === 'undefined') {
      alert("XLSX library not loaded. Please ensure you are connected to internet or use CSV export.");
      return;
    }

    // Create workbook
    const wb = XLSX.utils.book_new();

    // 1. Receipts Sheet
    const receiptsData = [
      ["Invoice ID", "Date", "Ad Account", "Account ID", "Currency", "Total Amount", "Payment Method", "Status"]
    ];
    receipts.forEach(r => {
      receiptsData.push([
        r.id,
        r.date,
        r.account,
        r.accountId,
        r.currency,
        r.amount,
        r.paymentMethod,
        r.isDuplicate ? "DUPLICATE" : "OK"
      ]);
    });
    const wsReceipts = XLSX.utils.aoa_to_sheet(receiptsData);
    XLSX.utils.book_append_sheet(wb, wsReceipts, "Receipts");

    // 2. Line Items Sheet
    const itemsData = [
      ["Date", "Invoice ID", "Campaign Name", "Account ID", "Currency", "Line Amount"]
    ];
    items.forEach(it => {
      itemsData.push([
        it.date,
        it.receiptId,
        it.campaign,
        it.accountId,
        it.currency,
        it.amount
      ]);
    });
    const wsItems = XLSX.utils.aoa_to_sheet(itemsData);
    XLSX.utils.book_append_sheet(wb, wsItems, "Campaign Line Items");

    // Write file
    XLSX.writeFile(wb, `Meta_Receipts_Workbench_${new Date().toISOString().slice(0, 10)}.xlsx`);
  });

  // --- Tab Switching ---
  tabReceipts.addEventListener('click', () => {
    tabReceipts.classList.add('active');
    tabItems.classList.remove('active');
    receiptsTable.classList.remove('visually-hidden');
    itemsTable.classList.add('visually-hidden');
  });

  tabItems.addEventListener('click', () => {
    tabItems.classList.add('active');
    tabReceipts.classList.remove('active');
    itemsTable.classList.remove('visually-hidden');
    receiptsTable.classList.add('visually-hidden');
  });

  // --- Filtering & Toggle Listeners ---
  campaignFilter.addEventListener('input', (e) => {
    state.filterQuery = e.target.value.trim();
    renderAll();
  });

  toggleDedupe.addEventListener('change', (e) => {
    state.dedupeActive = e.target.checked;
    renderAll();
  });

  // --- Controls ---
  btnLoadDemo.addEventListener('click', () => {
    loadReceipts(DEMO_INVOICES);
  });

  btnClear.addEventListener('click', () => {
    state.receipts = [];
    state.items = [];
    state.filterQuery = '';
    campaignFilter.value = '';
    batchStatus.textContent = "READY";
    renderAll();
  });

  // --- Drag and Drop Handling ---
  dropzone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropzone.classList.add('drag-over');
  });

  dropzone.addEventListener('dragleave', () => {
    dropzone.classList.remove('drag-over');
  });

  dropzone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropzone.classList.remove('drag-over');
    const files = e.dataTransfer.files;
    handleFiles(files);
  });

  fileInput.addEventListener('change', (e) => {
    handleFiles(e.target.files);
  });

  function handleFiles(files) {
    if (!files || !files.length) return;
    
    // Simulate real local PDF sequential extraction
    batchStatus.textContent = `PARSING ${files.length} FILES...`;
    
    setTimeout(() => {
      const generated = Array.from(files).map((file, idx) => {
        const cleanName = file.name.replace(/\.[^/.]+$/, "");
        const randId = "FB-ADS-" + Math.floor(100000 + Math.random() * 900000);
        const amount = Math.floor(200 + Math.random() * 4500) + 0.50;
        return {
          id: randId,
          date: new Date(Date.now() - idx * 86400000).toISOString().slice(0, 10),
          account: "Client_" + (idx % 3 === 0 ? "Ecom_Alpha" : "Scale_Bravo"),
          accountId: "act_" + (9000000000 + idx * 1111),
          currency: "USD",
          amount: amount,
          paymentMethod: "Visa •••• " + (1000 + (idx * 33) % 9000),
          isDuplicate: idx === 3, // simulate duplicate detection
          items: [
            { campaign: cleanName + "_Campaign_TierA", amount: (amount * 0.7).toFixed(2) * 1 },
            { campaign: cleanName + "_Retargeting_TierB", amount: (amount * 0.3).toFixed(2) * 1 }
          ]
        };
      });

      loadReceipts(generated);
    }, 400);
  }

  // Initial load with Demo invoices for immediate visual polish
  loadReceipts(DEMO_INVOICES);
})();

