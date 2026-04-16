import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

const APEX_SRC = "/App_Plugins/DcmsBackoffice/lib/apexcharts.min.js";

const datasets = {
  day: {
    labels: ["Feb 24", "Feb 28", "Mar 4", "Mar 8", "Mar 12", "Mar 16", "Mar 20", "Mar 24", "Mar 28", "Apr 3"],
    values: [1200, 1650, 3100, 2400, 6986, 4000, 2050, 4700, 3100, 6420],
  },
  month: {
    labels: ["Nov", "Dec", "Jan", "Feb", "Mar", "Apr"],
    values: [42000, 38500, 51200, 47800, 55600, 44800],
  },
};

function loadScript(src) {
  return new Promise((resolve, reject) => {
    if (document.querySelector(`script[src="${src}"]`)) {
      resolve();
      return;
    }
    const s = document.createElement("script");
    s.src = src;
    s.async = true;
    s.onload = () => resolve();
    s.onerror = () => reject(new Error("Failed to load " + src));
    document.head.appendChild(s);
  });
}

function formatMoney(n) {
  return "$" + Math.round(n).toLocaleString("en-US");
}

export default class DcmsSalesDashboardElement extends UmbElementMixin(HTMLElement) {
  #period = "day";
  #compare = false;
  #chart = null;

  constructor() {
    super();
    this.attachShadow({ mode: "open" });
  }

  async connectedCallback() {
    this.#renderShell();
    try {
      await loadScript(APEX_SRC);
    } catch {
      const host = this.shadowRoot?.querySelector(".apex-inner");
      if (host) host.innerHTML = '<p style="padding:14px;color:#b91c1c">Could not load chart library.</p>';
      return;
    }
    this.#mountChart();
    window.addEventListener("resize", this.#onResize);
  }

  disconnectedCallback() {
    window.removeEventListener("resize", this.#onResize);
    if (this.#chart) {
      this.#chart.destroy();
      this.#chart = null;
    }
  }

  #onResize = () => {
    if (!this.#chart) return;
    const h = this.#hostHeight();
    this.#chart.updateOptions({ chart: { height: h } }, false, false);
  };

  #hostHeight() {
    const host = this.shadowRoot?.querySelector(".apex-host");
    if (!host) return 260;
    const h = host.clientHeight;
    return h > 120 ? h : 260;
  }

  #buildOptions() {
    const ds = datasets[this.#period];
    const current = ds.values.slice();
    const previous = current.map((v, i) => Math.round(v * (0.82 + (i % 3) * 0.03)));
    const series = this.#compare
      ? [
          { name: "Current Period", data: current },
          { name: "Previous Period", data: previous },
        ]
      : [{ name: "Revenue", data: current }];

    return {
      chart: {
        type: "area",
        height: this.#hostHeight(),
        width: "100%",
        toolbar: { show: false },
        zoom: { enabled: false },
        animations: { enabled: true, speed: 350 },
        fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
        foreColor: "rgba(55, 65, 81, 0.65)",
      },
      series,
      xaxis: {
        categories: ds.labels.slice(),
        labels: { style: { fontSize: "11px", colors: "rgba(55, 65, 81, 0.65)" } },
        axisBorder: { show: true, color: "rgba(148, 163, 184, 0.35)" },
        axisTicks: { show: true, color: "rgba(148, 163, 184, 0.35)" },
      },
      yaxis: {
        labels: {
          style: { fontSize: "11px", colors: "rgba(55, 65, 81, 0.55)" },
          formatter: (val) => formatMoney(val),
        },
      },
      stroke: {
        curve: "smooth",
        width: this.#compare ? [3, 2] : 3,
        dashArray: this.#compare ? [0, 4] : 0,
        colors: this.#compare ? ["#aa0014", "#e5bdb9"] : ["#aa0014"],
      },
      fill: this.#compare
        ? {
            type: ["gradient", "solid"],
            gradient: {
              shadeIntensity: 1,
              opacityFrom: 0.28,
              opacityTo: 0.02,
              stops: [0, 88, 100],
            },
            opacity: [1, 0],
          }
        : {
            type: "gradient",
            gradient: { shadeIntensity: 1, opacityFrom: 0.28, opacityTo: 0.02, stops: [0, 88, 100] },
          },
      colors: this.#compare ? ["#aa0014", "#e5bdb9"] : ["#aa0014"],
      dataLabels: { enabled: false },
      markers: { size: 0, hover: { size: 5 } },
      grid: {
        borderColor: "rgba(148, 163, 184, 0.18)",
        padding: { left: 6, right: 14, top: 10, bottom: 6 },
      },
      legend: { show: false },
      tooltip: {
        theme: "light",
        y: { formatter: (val) => formatMoney(val) },
      },
    };
  }

  #renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        :host { display:block; box-sizing:border-box; }
        *, *::before, *::after { box-sizing:border-box; }

        .page {
          min-height: 100%;
          padding: 24px;
          font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
          color: #1f2937;
        }

        .header {
          display:flex;
          align-items:flex-end;
          justify-content:space-between;
          gap:16px;
          margin-bottom: 24px;
        }
        .title { margin:0; font-size: 22px; font-weight: 800; letter-spacing: -0.02em; color:#111827; }
        .subtitle { margin:6px 0 0; font-size: 13px; color: rgba(55, 65, 81, 0.75); }
        .header-actions { display:flex; gap:8px; flex-wrap:wrap; justify-content:flex-end; }
        .btn {
          appearance:none;
          border:1px solid rgba(148, 163, 184, 0.55);
          background:#ffffff;
          color:#111827;
          border-radius: 10px;
          padding: 6px 12px;
          font-size: 12px;
          font-weight: 700;
          cursor:pointer;
          transition: background-color .15s ease, transform .05s ease, border-color .15s ease;
        }
        .btn:hover { background:#fafafa; }
        .btn:active { transform: translateY(0.5px); }
        .btn-primary {
          border-color: transparent;
          background: #aa0014;
          color: #ffffff;
        }
        .btn-primary:hover { background: #8f0011; }

        .grid { display:grid; grid-template-columns: repeat(12, minmax(0, 1fr)); gap: 16px; }
        .span-12 { grid-column: span 12; }
        @media (min-width: 880px) {
          .span-4 { grid-column: span 4; }
          .span-8 { grid-column: span 8; }
        }

        .card {
          border: 1px solid rgba(148, 163, 184, 0.25);
          border-radius: 14px;
          overflow:hidden;
          background: #ffffff;
        }
        .card.soft { background: rgba(241, 245, 249, 0.7); }
        .card.pad { padding: 16px; }

        .card-head {
          display:flex;
          align-items:center;
          justify-content:space-between;
          gap:12px;
          padding: 14px 16px;
          border-bottom: 1px solid rgba(148, 163, 184, 0.25);
          background: rgba(241, 245, 249, 0.55);
        }
        .card-head h3 { margin:0; font-size: 13px; font-weight: 800; color:#111827; }
        .head-meta { display:flex; align-items:center; gap:12px; font-size: 10px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.12em; color: rgba(55, 65, 81, 0.7); }
        .dot { width:8px; height:8px; border-radius:999px; display:inline-block; }

        .chart-area { position:relative; padding: 16px; height: 260px; }
        .apex-host { height: 100%; min-height: 240px; }
        .apex-inner { width: 100%; height: 100%; }

        .two-col { display:grid; grid-template-columns: 1fr; gap: 12px; }
        @media (min-width: 880px) { .two-col { grid-template-columns: 1fr 1fr; } }

        .muted { color: rgba(55, 65, 81, 0.7); }
        .pill {
          display:inline-flex; align-items:center; gap:6px;
          padding: 4px 8px;
          border-radius: 999px;
          font-size: 10px;
          font-weight: 800;
          text-transform: uppercase;
          letter-spacing: 0.12em;
          background: rgba(170,0,20,0.10);
          color:#aa0014;
        }

        .rows { display:flex; flex-direction:column; gap: 14px; }
        .row-head { display:flex; align-items:flex-end; justify-content:space-between; gap:12px; margin-bottom: 6px; }
        .bar { height: 14px; border-radius: 999px; overflow:hidden; background: rgba(255,255,255,0.55); border: 1px solid rgba(148, 163, 184, 0.18); }
        .bar > span { display:block; height:100%; }

        table { width:100%; border-collapse: collapse; }
        thead th {
          font-size: 10px;
          font-weight: 800;
          text-transform: uppercase;
          letter-spacing: 0.08em;
          color: rgba(55, 65, 81, 0.65);
          padding: 10px 14px;
          background: rgba(241, 245, 249, 0.55);
          border-bottom: 1px solid rgba(148, 163, 184, 0.22);
        }
        tbody td { padding: 10px 14px; font-size: 12px; border-bottom: 1px solid rgba(148, 163, 184, 0.14); }
        tbody tr:hover { background: rgba(241, 245, 249, 0.55); }

        .dark {
          background: #09090b;
          color: #ffffff;
          border: 1px solid rgba(170,0,20,0.15);
          box-shadow: 0 18px 40px rgba(80, 10, 18, 0.18);
        }
        .dark .muted { color: rgba(161, 161, 170, 0.9); }
        .dark .dark-pill { color: rgba(113, 113, 122, 1); font-size: 10px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.12em; }
        .tier { cursor:pointer; }
        .tier-head { display:flex; align-items:center; justify-content:space-between; gap:12px; margin-bottom:8px; }
        .tier-left { display:flex; align-items:center; gap:12px; }
        .tier-icon {
          width:32px; height:32px; border-radius: 10px;
          background: rgba(255,255,255,0.06);
          display:flex; align-items:center; justify-content:center;
          transition: background-color .15s ease;
        }
        .tier:hover .tier-icon { background: rgba(170,0,20,0.18); }
        .tier-name { margin:0; font-size: 12px; font-weight: 800; }
        .tier-sub { margin:2px 0 0; font-size: 10px; color: rgba(113, 113, 122, 1); font-weight: 700; }
        .tier-amt { margin:0; font-size: 14px; font-weight: 900; }
        .tier-delta { margin:2px 0 0; font-size: 10px; font-weight: 800; }
        .tier-bar { height: 6px; background: rgba(255,255,255,0.10); border-radius: 999px; overflow:hidden; }
        .tier-bar > span { display:block; height:100%; background:#aa0014; }

        .orders { padding: 8px; display:flex; flex-direction:column; gap: 6px; }
        .order {
          display:flex; justify-content:space-between; align-items:center; gap:12px;
          padding: 10px 12px;
          border-radius: 12px;
          background: rgba(255,255,255,0.55);
          border: 1px solid rgba(148, 163, 184, 0.12);
        }
        .order.primary { background:#ffffff; }
        .order-id { margin:0; font-size: 11px; font-weight: 900; color:#111827; }
        .order-name { margin:2px 0 0; font-size: 9px; font-weight: 700; color: rgba(55,65,81,0.65); }
        .order-amt { margin:0; font-size: 11px; font-weight: 900; color:#111827; text-align:right; }
        .order-status { margin:2px 0 0; font-size: 8px; font-weight: 900; text-transform: uppercase; letter-spacing: 0.12em; text-align:right; }

        .footer {
          margin-top: 28px;
          padding-top: 18px;
          border-top: 1px solid rgba(148, 163, 184, 0.22);
          display:grid;
          grid-template-columns: repeat(2, minmax(0, 1fr));
          gap: 14px;
        }
        @media (min-width: 880px) { .footer { grid-template-columns: repeat(4, minmax(0, 1fr)); } }
        .metric-k { font-size: 10px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.10em; color: rgba(55,65,81,0.65); }
        .metric-v { margin-top: 4px; font-size: 13px; font-weight: 900; }
      </style>
      <main class="page" aria-label="Dashboard">
        <header class="header">
          <div>
            <h1 class="title">Caseflow Pro Executive Hub</h1>
            <p class="subtitle">Real-time enterprise performance monitoring and strategic oversight.</p>
          </div>
          <div class="header-actions">
            <button type="button" class="btn" data-action="period">
              ${this.#period === "day" ? "Last 30 Days" : "Last 6 Months"}
            </button>
            <button type="button" class="btn btn-primary" data-action="compare">
              ${this.#compare ? "Comparing" : "Compare"}
            </button>
          </div>
        </header>

        <div class="grid">
          <!-- 1. Sales Performance Chart -->
          <section class="card" style="grid-column: span 12;">
            <div class="card-head">
              <div style="display:flex;align-items:center;gap:10px;">
                <span class="dot" style="background:#aa0014"></span>
                <h3>Revenue Performance Trend</h3>
              </div>
              <div class="head-meta">
                <span style="display:inline-flex;align-items:center;gap:6px;">
                  <span class="dot" style="background:#aa0014"></span> Current Period
                </span>
                <span style="display:inline-flex;align-items:center;gap:6px;">
                  <span class="dot" style="background:#d4d4d8"></span> Previous Period
                </span>
              </div>
            </div>
            <div class="chart-area">
              <div class="apex-host"><div class="apex-inner"></div></div>
            </div>
          </section>

          <!-- 2. Membership Mix -->
          <section class="card soft pad span-12 span-4">
            <h3 style="margin:0 0 16px;font-size:13px;font-weight:800;color:#111827;">Membership Mix</h3>
            <div style="display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center;">
              <div style="position:relative;width:160px;height:160px;">
                <svg style="width:100%;height:100%;transform:rotate(-90deg)" viewBox="0 0 36 36" aria-hidden="true">
                  <circle cx="18" cy="18" r="16" fill="transparent" stroke="#fbdbd8" stroke-width="4"></circle>
                  <circle cx="18" cy="18" r="16" fill="transparent" stroke="#aa0014" stroke-dasharray="72 100" stroke-width="4"></circle>
                </svg>
                <div style="position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;">
                  <div style="font-size:22px;font-weight:900;color:#aa0014;">72%</div>
                  <div style="font-size:9px;font-weight:900;text-transform:uppercase;color:rgba(55,65,81,0.7);letter-spacing:0.12em;">Registered</div>
                </div>
              </div>
              <div style="margin-top:18px;width:100%;display:flex;flex-direction:column;gap:10px;">
                <div style="display:flex;justify-content:space-between;align-items:center;font-size:12px;">
                  <span style="display:flex;align-items:center;gap:10px;"><span class="dot" style="background:#aa0014"></span> Registered</span>
                  <span style="font-weight:900;">$428,930</span>
                </div>
                <div style="display:flex;justify-content:space-between;align-items:center;font-size:12px;color:rgba(55,65,81,0.7);">
                  <span style="display:flex;align-items:center;gap:10px;"><span class="dot" style="background:#e5e7eb"></span> Guest</span>
                  <span style="font-weight:800;">$152,400</span>
                </div>
              </div>
            </div>
          </section>

          <!-- 3. Application Source Distribution -->
          <section class="card soft pad span-12 span-8">
            <div style="display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:16px;">
              <h3 style="margin:0;font-size:13px;font-weight:800;color:#111827;">Application Source Distribution</h3>
              <span class="pill">Live Traffic</span>
            </div>
            <div class="rows">
              <div>
                <div class="row-head">
                  <div style="display:flex;align-items:center;gap:10px;">
                    <span class="dot" style="background:#aa0014"></span>
                    <span style="font-size:12px;font-weight:900;text-transform:uppercase;letter-spacing:0.06em;">Mobile App</span>
                  </div>
                  <div style="font-size:14px;font-weight:900;color:#aa0014;">64% <span class="muted" style="font-size:10px;font-weight:700;">($372k)</span></div>
                </div>
                <div class="bar"><span style="width:64%;background:#aa0014;"></span></div>
              </div>
              <div>
                <div class="row-head">
                  <div style="display:flex;align-items:center;gap:10px;">
                    <span class="dot" style="background:#64748b"></span>
                    <span style="font-size:12px;font-weight:900;text-transform:uppercase;letter-spacing:0.06em;">Web Portal</span>
                  </div>
                  <div style="font-size:14px;font-weight:900;color:#111827;">28% <span class="muted" style="font-size:10px;font-weight:700;">($162k)</span></div>
                </div>
                <div class="bar"><span style="width:28%;background:#ef4444;"></span></div>
              </div>
              <div>
                <div class="row-head">
                  <div style="display:flex;align-items:center;gap:10px;">
                    <span class="dot" style="background:#94a3b8"></span>
                    <span style="font-size:12px;font-weight:900;text-transform:uppercase;letter-spacing:0.06em;">In-Store POS</span>
                  </div>
                  <div style="font-size:14px;font-weight:900;color:#111827;">8% <span class="muted" style="font-size:10px;font-weight:700;">($46k)</span></div>
                </div>
                <div class="bar"><span style="width:8%;background:#cbd5e1;"></span></div>
              </div>
            </div>
          </section>

          <!-- Campaign Oversight -->
          <section class="card" style="grid-column: span 12;">
            <div class="card-head" style="background: rgba(241,245,249,0.35);">
              <h3 style="font-size:11px; text-transform: uppercase; letter-spacing: 0.14em; color:#aa0014;">Campaign Oversight</h3>
              <span style="font-size:10px;font-weight:900;color:rgba(55,65,81,0.7);">LIVE UPDATE</span>
            </div>
            <div style="overflow:auto;">
              <table>
                <thead>
                  <tr>
                    <th style="text-align:left;">Name</th>
                    <th style="text-align:center;">Status</th>
                    <th style="text-align:right;">Reach</th>
                    <th style="text-align:right;">ROI</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td style="font-weight:700;">Q4 Premium Upsell</td>
                    <td style="text-align:center;"><span style="padding:2px 8px;border-radius:999px;background:#dcfce7;color:#15803d;font-size:9px;font-weight:900;">ACTIVE</span></td>
                    <td style="text-align:right;color:rgba(55,65,81,0.7);">1.2M</td>
                    <td style="text-align:right;font-weight:900;color:#aa0014;">24.8%</td>
                  </tr>
                  <tr>
                    <td style="font-weight:700;">Holiday Season Flash</td>
                    <td style="text-align:center;"><span style="padding:2px 8px;border-radius:999px;background:#dcfce7;color:#15803d;font-size:9px;font-weight:900;">ACTIVE</span></td>
                    <td style="text-align:right;color:rgba(55,65,81,0.7);">850K</td>
                    <td style="text-align:right;font-weight:900;color:#aa0014;">18.2%</td>
                  </tr>
                  <tr>
                    <td style="font-weight:700;">Tier Migration Program</td>
                    <td style="text-align:center;"><span style="padding:2px 8px;border-radius:999px;background:#fef3c7;color:#b45309;font-size:9px;font-weight:900;text-transform:uppercase;">Optimizing</span></td>
                    <td style="text-align:right;color:rgba(55,65,81,0.7);">420K</td>
                    <td style="text-align:right;font-weight:900;color:#aa0014;">12.5%</td>
                  </tr>
                  <tr>
                    <td style="font-weight:700;">New Market Entry - APAC</td>
                    <td style="text-align:center;"><span style="padding:2px 8px;border-radius:999px;background:#dcfce7;color:#15803d;font-size:9px;font-weight:900;">ACTIVE</span></td>
                    <td style="text-align:right;color:rgba(55,65,81,0.7);">3.1M</td>
                    <td style="text-align:right;font-weight:900;color:#aa0014;">31.4%</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <!-- Membership Tier Velocity -->
          <section class="card dark pad" style="grid-column: span 12;">
            <div style="display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:16px;">
              <h3 style="margin:0;font-size:13px;font-weight:900;color:#aa0014;">Membership Tier Velocity</h3>
              <span class="dark-pill">Revenue Target</span>
            </div>
            <div style="display:flex;flex-direction:column;gap:18px;">
              <div class="tier">
                <div class="tier-head">
                  <div class="tier-left">
                    <div class="tier-icon"><span style="color:#aa0014;font-weight:900;">P</span></div>
                    <div>
                      <p class="tier-name">Platinum Elite</p>
                      <p class="tier-sub">Avg. Order: $280</p>
                    </div>
                  </div>
                  <div style="text-align:right;">
                    <p class="tier-amt">$244,150</p>
                    <p class="tier-delta" style="color:#4ade80;">+12.5% ▲</p>
                  </div>
                </div>
                <div class="tier-bar"><span style="width:85%;"></span></div>
              </div>
              <div class="tier">
                <div class="tier-head">
                  <div class="tier-left">
                    <div class="tier-icon"><span style="color:#eab308;font-weight:900;">G</span></div>
                    <div>
                      <p class="tier-name">Gold Preferred</p>
                      <p class="tier-sub">Avg. Order: $145</p>
                    </div>
                  </div>
                  <div style="text-align:right;">
                    <p class="tier-amt">$203,400</p>
                    <p class="tier-delta" style="color:rgba(113,113,122,1);">STABLE</p>
                  </div>
                </div>
                <div class="tier-bar"><span style="width:62%;background:#ca8a04;"></span></div>
              </div>
              <div class="tier">
                <div class="tier-head">
                  <div class="tier-left">
                    <div class="tier-icon"><span style="color:#a1a1aa;font-weight:900;">S</span></div>
                    <div>
                      <p class="tier-name">Silver Rewards</p>
                      <p class="tier-sub">Avg. Order: $88</p>
                    </div>
                  </div>
                  <div style="text-align:right;">
                    <p class="tier-amt">$133,780</p>
                    <p class="tier-delta" style="color:#f87171;">-2.1% ▼</p>
                  </div>
                </div>
                <div class="tier-bar"><span style="width:44%;background:#71717a;"></span></div>
              </div>
            </div>
          </section>

          <!-- Recent Orders -->
          <section class="card soft" style="grid-column: span 12;">
            <div class="card-head" style="background: transparent;">
              <h3 style="font-size:11px;text-transform:uppercase;letter-spacing:0.12em;">Recent Orders</h3>
              <a href="#" style="font-size:10px;font-weight:900;color:#aa0014;text-decoration:none;">VIEW ALL</a>
            </div>
            <div class="orders">
              <div class="order primary">
                <div>
                  <p class="order-id">#ORD-9902</p>
                  <p class="order-name">Jameson Pierce</p>
                </div>
                <div>
                  <p class="order-amt">$1,450.00</p>
                  <p class="order-status" style="color:#059669;">Processing</p>
                </div>
              </div>
              <div class="order">
                <div>
                  <p class="order-id">#ORD-9901</p>
                  <p class="order-name">Elena Rodriguez</p>
                </div>
                <div>
                  <p class="order-amt">$820.50</p>
                  <p class="order-status" style="color:#16a34a;">Shipped</p>
                </div>
              </div>
              <div class="order">
                <div>
                  <p class="order-id">#ORD-9900</p>
                  <p class="order-name">Sarah Jenkins</p>
                </div>
                <div>
                  <p class="order-amt">$2,100.00</p>
                  <p class="order-status" style="color:#059669;">Processing</p>
                </div>
              </div>
            </div>
          </section>

          <!-- Top 5 SKU Performance -->
          <section class="card soft" style="grid-column: span 12;">
            <div class="card-head" style="background: transparent;">
              <h3 style="font-size:11px; text-transform: uppercase; letter-spacing: 0.14em; color:#aa0014;">Top 5 SKU Performance</h3>
            </div>
            <div style="overflow:auto;">
              <table>
                <thead>
                  <tr>
                    <th style="text-align:left;">Product</th>
                    <th style="text-align:left;">Category</th>
                    <th style="text-align:right;">Units</th>
                    <th style="text-align:right;">Revenue</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td style="font-weight:900;">Echelon Watch</td>
                    <td class="muted" style="font-weight:700;">Accessories</td>
                    <td style="text-align:right;font-weight:800;">12.4k</td>
                    <td style="text-align:right;font-weight:900;color:#aa0014;">$890k</td>
                  </tr>
                  <tr>
                    <td style="font-weight:900;">Velocity Shoe</td>
                    <td class="muted" style="font-weight:700;">Footwear</td>
                    <td style="text-align:right;font-weight:800;">8.1k</td>
                    <td style="text-align:right;font-weight:900;color:#aa0014;">$640k</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <!-- Market share + Strategic Brand Exposure -->
          <section class="card soft pad" style="grid-column: span 12;">
            <div class="two-col">
              <div class="card soft pad" style="border:none;background:transparent;">
                <h3 style="margin:0 0 12px;font-size:11px;font-weight:900;text-transform:uppercase;letter-spacing:0.12em;">Market Share by Category</h3>
                <div style="display:flex;flex-direction:column;gap:10px;">
                  <div class="order primary" style="align-items:flex-start;">
                    <div>
                      <p style="margin:0;font-size:12px;font-weight:900;">Footwear</p>
                      <p class="muted" style="margin:3px 0 0;font-size:10px;font-weight:700;">1,420 Active Products</p>
                    </div>
                    <div style="text-align:right;">
                      <p style="margin:0;font-size:12px;font-weight:900;color:#aa0014;">98.2 Index</p>
                      <div style="display:flex;gap:3px;justify-content:flex-end;margin-top:6px;">
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                      </div>
                    </div>
                  </div>
                  <div class="order" style="align-items:flex-start;">
                    <div>
                      <p style="margin:0;font-size:12px;font-weight:900;">Electronics</p>
                      <p class="muted" style="margin:3px 0 0;font-size:10px;font-weight:700;">890 Active Products</p>
                    </div>
                    <div style="text-align:right;">
                      <p style="margin:0;font-size:12px;font-weight:900;color:#aa0014;">94.5 Index</p>
                      <div style="display:flex;gap:3px;justify-content:flex-end;margin-top:6px;">
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                        <span style="width:4px;height:12px;background:#aa0014;border-radius:999px;"></span>
                        <span style="width:4px;height:12px;background:#e5e7eb;border-radius:999px;"></span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <div class="card soft pad" style="border:none;background:transparent;">
                <h3 style="margin:0 0 12px;font-size:11px;font-weight:900;text-transform:uppercase;letter-spacing:0.12em;">Strategic Brand Exposure</h3>
                <table>
                  <thead>
                    <tr>
                      <th style="text-align:left;">Brand</th>
                      <th style="text-align:right;">Products</th>
                      <th style="text-align:right;">Share</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td style="font-weight:900;">LuxeCore Systems</td>
                      <td style="text-align:right;">412</td>
                      <td style="text-align:right;font-weight:900;color:#aa0014;">24.5%</td>
                    </tr>
                    <tr>
                      <td style="font-weight:900;">Vanguard Athletic</td>
                      <td style="text-align:right;">389</td>
                      <td style="text-align:right;font-weight:900;color:#aa0014;">18.2%</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        </div>

        <footer class="footer" aria-label="System metrics">
          <div>
            <div class="metric-k">System Uptime</div>
            <div class="metric-v" style="color:#16a34a;">99.98%</div>
          </div>
          <div>
            <div class="metric-k">Active Executives</div>
            <div class="metric-v">12 Available</div>
          </div>
          <div>
            <div class="metric-k">Global Latency</div>
            <div class="metric-v">14ms</div>
          </div>
          <div>
            <div class="metric-k">Data Freshness</div>
            <div class="metric-v">Just Now</div>
          </div>
        </footer>
      </main>
    `;

    const btnPeriod = this.shadowRoot?.querySelector('[data-action="period"]');
    const btnCompare = this.shadowRoot?.querySelector('[data-action="compare"]');

    btnPeriod?.addEventListener("click", () => {
      this.#period = this.#period === "day" ? "month" : "day";
      this.#renderShell();
      this.#mountChart();
    });

    btnCompare?.addEventListener("click", () => {
      this.#compare = !this.#compare;
      this.#renderShell();
      this.#mountChart();
    });
  }

  #mountChart() {
    const ApexCharts = window.ApexCharts;
    const el = this.shadowRoot?.querySelector(".apex-inner");
    if (!ApexCharts || !el) return;

    if (this.#chart) {
      this.#chart.destroy();
      this.#chart = null;
    }

    this.#chart = new ApexCharts(el, this.#buildOptions());
    this.#chart.render();
  }
}

customElements.define("dcms-sales-dashboard", DcmsSalesDashboardElement);
