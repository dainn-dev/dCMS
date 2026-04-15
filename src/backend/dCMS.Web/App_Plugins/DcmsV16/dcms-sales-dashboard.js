import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

const APEX_SRC = "/App_Plugins/DcmsBackoffice/lib/apexcharts.min.js";

const widgets = {
  newOrders: [
    { orderNumber: "S$2504020012", amount: "S$30.00" },
    { orderNumber: "S$2504020011", amount: "S$20.00" },
    { orderNumber: "S$2504020010", amount: "S$94.00" },
    { orderNumber: "S$2504020009", amount: "S$20.00" },
    { orderNumber: "S$2504020008", amount: "S$44.00" },
  ],
  topProducts: [
    { name: "Home Department Hust...", amount: "S$6,687.00" },
    { name: "Jellycat Bashful Thr...", amount: "S$2,971.38" },
    { name: "TANGS e-Gift Card", amount: "S$1,420.00" },
    { name: "Jo", amount: "S$958.20" },
    { name: "HAPPY SOCKS X BEATLE...", amount: "S$636.00" },
  ],
  topCategories: [
    { name: "STAFF-DISCOUNTS", amount: "S$9,258.20" },
    { name: "HOME", amount: "S$7,840.20" },
    { name: "BIG DISCOUNT", amount: "S$5,968.10" },
    { name: "Bed & Bath", amount: "S$5,777.10" },
    { name: "KIDS", amount: "S$3,304.68" },
  ],
  topBrands: [
    { name: "River Home", amount: "S$6,687.00" },
    { name: "Jellycat", amount: "S$2,971.38" },
    { name: "TANGS eGift Card", amount: "S$1,420.00" },
    { name: "Dyson", amount: "S$958.20" },
    { name: "Tom Ford", amount: "S$928.00" },
  ],
};

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
  return "S$" + Math.round(n).toLocaleString("en-SG");
}

export default class DcmsSalesDashboardElement extends UmbElementMixin(HTMLElement) {
  #period = "day";
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
      this.shadowRoot.querySelector(".apex-inner").innerHTML =
        '<p style="padding:1rem;color:#b91c1c">Could not load chart library.</p>';
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
    const host = this.shadowRoot.querySelector(".apex-host");
    if (!host) return 300;
    const h = host.clientHeight;
    return h > 80 ? h : 300;
  }

  #buildOptions() {
    const ds = datasets[this.#period];
    const day = this.#period === "day";
    return {
      chart: {
        type: "area",
        height: this.#hostHeight(),
        width: "100%",
        toolbar: { show: false },
        zoom: { enabled: false },
        animations: { enabled: true, speed: 350 },
        fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
        foreColor: "#64748b",
      },
      series: [{ name: "Sales", data: ds.values.slice() }],
      xaxis: {
        categories: ds.labels.slice(),
        labels: {
          style: { fontSize: "11px", colors: "#64748b" },
          rotate: day ? -40 : 0,
          rotateAlways: day,
          hideOverlappingLabels: true,
        },
        axisBorder: { show: true, color: "#e2e8f0" },
        axisTicks: { show: true, color: "#e2e8f0" },
        title: {
          text: day ? "Days (sample)" : "Months (sample)",
          style: { fontSize: "11px", fontWeight: 500, color: "#64748b" },
        },
      },
      yaxis: {
        title: {
          text: "Sales (S$)",
          style: { fontSize: "11px", fontWeight: 500, color: "#64748b" },
        },
        labels: {
          style: { fontSize: "11px", colors: "#94a3b8" },
          formatter: (val) => formatMoney(val),
        },
      },
      stroke: { curve: "smooth", width: 2.5, colors: ["#d42054"] },
      fill: {
        type: "gradient",
        gradient: { shadeIntensity: 1, opacityFrom: 0.5, opacityTo: 0.04, stops: [0, 88, 100] },
      },
      colors: ["#d42054"],
      dataLabels: { enabled: false },
      markers: { size: 0, hover: { size: 5 } },
      grid: {
        borderColor: "#f1f5f9",
        strokeDashArray: 0,
        padding: { left: 4, right: 12, top: 8, bottom: 4 },
      },
      legend: { show: false },
      tooltip: {
        theme: "light",
        y: { formatter: (val) => formatMoney(val) },
      },
    };
  }

  #renderShell() {
    const list = (items, primary) =>
      items
        .map(
          (r) =>
            `<div style="display:flex;justify-content:space-between;gap:0.5rem;padding:0.35rem 0;border-bottom:1px solid #f1f5f9;font-size:0.8rem;">
            <span style="min-width:0;overflow:hidden;text-overflow:ellipsis">${r[primary]}</span>
            <strong style="flex-shrink:0">${r.amount}</strong>
          </div>`
        )
        .join("");

    this.shadowRoot.innerHTML = `
      <style>
        :host { display:block; box-sizing:border-box; }
        .shell { padding: 0.75rem clamp(0.75rem, 2.5vw, 1.75rem) 2rem; font-family: system-ui, sans-serif; }
        .main { max-width: 70rem; margin: 0 auto; }
        .toolbar { display:flex; align-items:center; justify-content:flex-end; flex-wrap:wrap; gap:0.75rem; margin-bottom:1rem; }
        .toolbar span { margin-right:auto; font-size:0.8rem; color:#6e6e6e; }
        .toggle { display:inline-flex; border-radius:3px; overflow:hidden; border:1px solid #d8d7d9; }
        .toggle button { border:none; background:#fff; padding:0.4rem 1rem; cursor:pointer; font-size:0.85rem; }
        .toggle button.on { background:#f5c1bc; color:#1b264f; font-weight:600; }
        .chart-wrap { background:#fff; border:1px solid #e9e9eb; border-radius:6px; margin-bottom:1.25rem; overflow:hidden; }
        .apex-host { min-height: clamp(220px, min(32vh, 28vw), 400px); height: clamp(220px, min(32vh, 28vw), 400px); }
        .apex-inner { width:100%; height:100%; box-sizing:border-box; }
        .kpi-grid { display:grid; grid-template-columns: repeat(auto-fit, minmax(min(100%,220px),1fr)); gap:1rem; }
        .kpi { background:#fff; border:1px solid #e9e9eb; border-radius:6px; padding:1rem; }
        .kpi h4 { margin:0 0 0.5rem; font-size:0.85rem; color:#1b264f; }
      </style>
      <div class="shell"><div class="main">
        <div class="toolbar">
          <span>Sales dashboard (sample data — connect APIs as needed)</span>
          <div class="toggle">
            <button type="button" class="${this.#period === "day" ? "on" : ""}" data-p="day">By day</button>
            <button type="button" class="${this.#period === "month" ? "on" : ""}" data-p="month">By month</button>
          </div>
        </div>
        <div class="chart-wrap"><div class="apex-host"><div class="apex-inner"></div></div></div>
        <div class="kpi-grid">
          <div class="kpi"><h4>New orders</h4><div>${list(widgets.newOrders, "orderNumber")}</div></div>
          <div class="kpi"><h4>Top products</h4><div>${list(widgets.topProducts, "name")}</div></div>
          <div class="kpi"><h4>Top categories</h4><div>${list(widgets.topCategories, "name")}</div></div>
          <div class="kpi"><h4>Top brands</h4><div>${list(widgets.topBrands, "name")}</div></div>
        </div>
      </div></div>`;

    this.shadowRoot.querySelectorAll(".toggle button").forEach((b) => {
      b.addEventListener("click", () => {
        this.#period = b.getAttribute("data-p");
        this.#renderShell();
        this.#mountChart();
      });
    });
  }

  #mountChart() {
    const ApexCharts = window.ApexCharts;
    const el = this.shadowRoot.querySelector(".apex-inner");
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
