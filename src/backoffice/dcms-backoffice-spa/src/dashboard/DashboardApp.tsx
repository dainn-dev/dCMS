import { useEffect, useMemo, useRef, useState } from "react";

const APEX_SRC = "/App_Plugins/DcmsBackoffice/lib/apexcharts.min.js";

type Period = "day" | "month";

const datasets: Record<Period, { labels: string[]; values: number[] }> = {
  day: {
    labels: ["Feb 24", "Feb 28", "Mar 4", "Mar 8", "Mar 12", "Mar 16", "Mar 20", "Mar 24", "Mar 28", "Apr 3"],
    values: [1200, 1650, 3100, 2400, 6986, 4000, 2050, 4700, 3100, 6420],
  },
  month: {
    labels: ["Nov", "Dec", "Jan", "Feb", "Mar", "Apr"],
    values: [42000, 38500, 51200, 47800, 55600, 44800],
  },
};

function formatMoney(n: number) {
  return "$" + Math.round(n).toLocaleString("en-US");
}

async function loadScriptOnce(src: string) {
  if (document.querySelector(`script[src="${src}"]`)) return;
  await new Promise<void>((resolve, reject) => {
    const s = document.createElement("script");
    s.src = src;
    s.async = true;
    s.onload = () => resolve();
    s.onerror = () => reject(new Error("Failed to load " + src));
    document.head.appendChild(s);
  });
}

type ApexCtor = new (el: Element, opts: unknown) => {
  render: () => void;
  destroy: () => void;
  updateOptions: (...args: any[]) => void;
};

function getApexCharts(): ApexCtor | null {
  const g = globalThis as any;
  return typeof g.ApexCharts === "function" ? (g.ApexCharts as ApexCtor) : null;
}

export function DashboardApp() {
  const [period, setPeriod] = useState<Period>("day");
  const [compare, setCompare] = useState(false);
  const [chartReady, setChartReady] = useState(false);
  const chartElRef = useRef<HTMLDivElement | null>(null);
  const chartRef = useRef<null | { destroy: () => void; updateOptions: (...args: any[]) => void }>(null);

  const options = useMemo(() => {
    const ds = datasets[period];
    const current = ds.values.slice();
    const previous = current.map((v, i) => Math.round(v * (0.82 + (i % 3) * 0.03)));
    const series = compare
      ? [
          { name: "Current Period", data: current },
          { name: "Previous Period", data: previous },
        ]
      : [{ name: "Revenue", data: current }];

    return {
      chart: {
        type: "area",
        height: "100%",
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
          formatter: (val: number) => formatMoney(val),
        },
      },
      stroke: {
        curve: "smooth",
        width: compare ? [3, 2] : 3,
        dashArray: compare ? [0, 4] : 0,
        colors: compare ? ["#aa0014", "#e5bdb9"] : ["#aa0014"],
      },
      fill: compare
        ? {
            type: ["gradient", "solid"],
            gradient: { shadeIntensity: 1, opacityFrom: 0.28, opacityTo: 0.02, stops: [0, 88, 100] },
            opacity: [1, 0],
          }
        : { type: "gradient", gradient: { shadeIntensity: 1, opacityFrom: 0.28, opacityTo: 0.02, stops: [0, 88, 100] } },
      colors: compare ? ["#aa0014", "#e5bdb9"] : ["#aa0014"],
      dataLabels: { enabled: false },
      markers: { size: 0, hover: { size: 5 } },
      grid: { borderColor: "rgba(148, 163, 184, 0.18)", padding: { left: 6, right: 14, top: 10, bottom: 6 } },
      legend: { show: false },
      tooltip: { theme: "light", y: { formatter: (val: number) => formatMoney(val) } },
    };
  }, [period, compare]);

  useEffect(() => {
    let cancelled = false;
    loadScriptOnce(APEX_SRC)
      .then(() => {
        if (cancelled) return;
        setChartReady(true);
      })
      .catch(() => {
        if (cancelled) return;
        setChartReady(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    const el = chartElRef.current;
    if (!chartReady || !el) return;

    const ApexCharts = getApexCharts();
    if (!ApexCharts) return;

    chartRef.current?.destroy();
    chartRef.current = null;

    const chart = new ApexCharts(el, options);
    chart.render();
    chartRef.current = chart;

    const onResize = () => chart.updateOptions({ chart: { height: "100%" } }, false, false);
    window.addEventListener("resize", onResize);

    return () => {
      window.removeEventListener("resize", onResize);
      chart.destroy();
    };
  }, [chartReady, options]);

  return (
    <main className="dcmsDashboard page" aria-label="Dashboard">
      <header className="header">
        <div>
          <h1 className="title">Overview</h1>
          <p className="subtitle">Real-time enterprise performance monitoring and strategic oversight.</p>
        </div>
        <div className="header-actions">
          <button type="button" className="btn" onClick={() => setPeriod(period === "day" ? "month" : "day")}>
            {period === "day" ? "Last 30 Days" : "Last 6 Months"}
          </button>
          <button type="button" className="btn btn-primary" onClick={() => setCompare((v) => !v)}>
            {compare ? "Comparing" : "Compare"}
          </button>
        </div>
      </header>

      <div className="grid">
        <section className="card span-12">
          <div className="card-head">
            <div className="head-left">
              <span className="dot dot-primary" aria-hidden />
              <h3>Revenue Performance Trend</h3>
            </div>
            <div className="head-meta" aria-label="Legend">
              <span className="meta-item">
                <span className="dot dot-primary" aria-hidden /> Current Period
              </span>
              <span className="meta-item">
                <span className="dot dot-muted" aria-hidden /> Previous Period
              </span>
            </div>
          </div>
          <div className="chart-area">
            <div className="apex-host">
              <div ref={chartElRef} className="apex-inner" />
              {!chartReady && <div className="chart-fallback">Loading chart…</div>}
            </div>
          </div>
        </section>

        <section className="card soft pad span-12 span-4">
          <h3 className="section-title">Membership Mix</h3>
          <div className="mix">
            <div className="donut" aria-hidden>
              <svg className="donut-svg" viewBox="0 0 36 36">
                <circle cx="18" cy="18" r="16" fill="transparent" stroke="#fbdbd8" strokeWidth="4" />
                <circle cx="18" cy="18" r="16" fill="transparent" stroke="#aa0014" strokeDasharray="72 100" strokeWidth="4" />
              </svg>
              <div className="donut-center">
                <div className="donut-pct">72%</div>
                <div className="donut-label">Registered</div>
              </div>
            </div>
            <div className="mix-rows">
              <div className="mix-row">
                <span className="mix-name">
                  <span className="dot dot-primary" aria-hidden /> Registered
                </span>
                <span className="mix-amt">$428,930</span>
              </div>
              <div className="mix-row muted">
                <span className="mix-name">
                  <span className="dot dot-light" aria-hidden /> Guest
                </span>
                <span className="mix-amt">$152,400</span>
              </div>
            </div>
          </div>
        </section>

        <section className="card soft pad span-12 span-8">
          <div className="app-head">
            <h3 className="section-title" style={{ margin: 0 }}>
              Application Source Distribution
            </h3>
            <span className="pill">Live Traffic</span>
          </div>
          <div className="rows">
            <div>
              <div className="row-head">
                <div className="row-left">
                  <span className="dot dot-primary" aria-hidden />
                  <span className="row-name">Mobile App</span>
                </div>
                <div className="row-val row-val-primary">
                  64% <span className="row-sub">($372k)</span>
                </div>
              </div>
              <div className="bar">
                <span style={{ width: "64%", background: "#aa0014" }} />
              </div>
            </div>
            <div>
              <div className="row-head">
                <div className="row-left">
                  <span className="dot dot-slate" aria-hidden />
                  <span className="row-name">Web Portal</span>
                </div>
                <div className="row-val">
                  28% <span className="row-sub">($162k)</span>
                </div>
              </div>
              <div className="bar">
                <span style={{ width: "28%", background: "#fe7c73" }} />
              </div>
            </div>
            <div>
              <div className="row-head">
                <div className="row-left">
                  <span className="dot dot-muted2" aria-hidden />
                  <span className="row-name">In-Store POS</span>
                </div>
                <div className="row-val">
                  8% <span className="row-sub">($46k)</span>
                </div>
              </div>
              <div className="bar">
                <span style={{ width: "8%", background: "#fbdbd8" }} />
              </div>
            </div>
          </div>
        </section>

        <section className="card span-12 span-lg-7">
          <div className="card-head card-head-soft">
            <h3 className="caps-primary">
              <span className="caps-icon" aria-hidden>
                <svg viewBox="0 0 24 24" focusable="false" aria-hidden="true">
                  <path
                    fill="currentColor"
                    d="M5 16.5V18H19v-1.5H5Zm0-3.75 3-3 3 3 6-6 1.06 1.06L11 14.81 8 11.81l-1.94 1.94L5 12.75Z"
                  />
                </svg>
              </span>
              Campaign Oversight
            </h3>
            <span className="live-badge">LIVE UPDATE</span>
          </div>
          <div className="table-wrap">
            <table className="table table-center">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Status</th>
                  <th>Reach</th>
                  <th>ROI</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td className="fw-semibold">Q4 Premium Upsell</td>
                  <td>
                    <span className="tag tag-ok">ACTIVE</span>
                  </td>
                  <td className="muted">1.2M</td>
                  <td className="roi">24.8%</td>
                </tr>
                <tr>
                  <td className="fw-semibold">Holiday Season Flash</td>
                  <td>
                    <span className="tag tag-ok">ACTIVE</span>
                  </td>
                  <td className="muted">850K</td>
                  <td className="roi">18.2%</td>
                </tr>
                <tr>
                  <td className="fw-semibold">Tier Migration Program</td>
                  <td>
                    <span className="tag tag-warn">Optimizing</span>
                  </td>
                  <td className="muted">420K</td>
                  <td className="roi">12.5%</td>
                </tr>
                <tr>
                  <td className="fw-semibold">New Market Entry - APAC</td>
                  <td>
                    <span className="tag tag-ok">ACTIVE</span>
                  </td>
                  <td className="muted">3.1M</td>
                  <td className="roi">31.4%</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section className="card card-dark pad span-12 span-lg-5">
          <div className="dark-head">
            <h3 className="dark-title">Membership Tier Velocity</h3>
            <span className="dark-subtitle">Revenue Target</span>
          </div>

          <div className="dark-list">
            <div className="tier">
              <div className="tier-row">
                <div className="tier-left">
                  <div className="tier-icon tier-icon-primary" aria-hidden>
                    P
                  </div>
                  <div>
                    <p className="tier-name">Platinum Elite</p>
                    <p className="tier-meta">Avg. Order: $280</p>
                  </div>
                </div>
                <div className="tier-right">
                  <p className="tier-amount">$244,150</p>
                  <p className="tier-delta tier-delta-up">+12.5% ▲</p>
                </div>
              </div>
              <div className="tier-bar">
                <span style={{ width: "85%" }} />
              </div>
            </div>

            <div className="tier">
              <div className="tier-row">
                <div className="tier-left">
                  <div className="tier-icon tier-icon-gold" aria-hidden>
                    G
                  </div>
                  <div>
                    <p className="tier-name">Gold Preferred</p>
                    <p className="tier-meta">Avg. Order: $145</p>
                  </div>
                </div>
                <div className="tier-right">
                  <p className="tier-amount">$203,400</p>
                  <p className="tier-delta tier-delta-stable">STABLE</p>
                </div>
              </div>
              <div className="tier-bar">
                <span style={{ width: "62%" }} className="tier-fill-gold" />
              </div>
            </div>

            <div className="tier">
              <div className="tier-row">
                <div className="tier-left">
                  <div className="tier-icon tier-icon-silver" aria-hidden>
                    S
                  </div>
                  <div>
                    <p className="tier-name">Silver Rewards</p>
                    <p className="tier-meta">Avg. Order: $88</p>
                  </div>
                </div>
                <div className="tier-right">
                  <p className="tier-amount">$133,780</p>
                  <p className="tier-delta tier-delta-down">-2.1% ▼</p>
                </div>
              </div>
              <div className="tier-bar">
                <span style={{ width: "44%" }} className="tier-fill-silver" />
              </div>
            </div>
          </div>
        </section>

        <section className="card span-12 span-lg-4">
          <div className="card-head card-head-soft">
            <h3 className="caps-primary">
              <span className="caps-icon" aria-hidden>
                <svg viewBox="0 0 24 24" focusable="false" aria-hidden="true">
                  <path
                    fill="currentColor"
                    d="M5 16.5V18H19v-1.5H5Zm0-3.75 3-3 3 3 6-6 1.06 1.06L11 14.81 8 11.81l-1.94 1.94L5 12.75Z"
                  />
                </svg>
              </span>
              Recent Orders
            </h3>
            <a className="link-primary" href="#">
              VIEW ALL
            </a>
          </div>
          <div className="card-body">
            <div className="orders orders-flush">
              <div className="order order-strong">
                <div>
                  <p className="order-id">#ORD-9902</p>
                  <p className="order-name">Jameson Pierce</p>
                </div>
                <div className="order-right">
                  <p className="order-amount">$1,450.00</p>
                  <p className="order-status order-status-processing">Processing</p>
                </div>
              </div>
              <div className="order">
                <div>
                  <p className="order-id">#ORD-9901</p>
                  <p className="order-name">Elena Rodriguez</p>
                </div>
                <div className="order-right">
                  <p className="order-amount">$820.50</p>
                  <p className="order-status order-status-shipped">Shipped</p>
                </div>
              </div>
              <div className="order">
                <div>
                  <p className="order-id">#ORD-9900</p>
                  <p className="order-name">Sarah Jenkins</p>
                </div>
                <div className="order-right">
                  <p className="order-amount">$2,100.00</p>
                  <p className="order-status order-status-processing">Processing</p>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="card span-12 span-lg-8">
          <div className="card-head card-head-soft">
            <h3 className="caps-primary">
              <span className="caps-icon" aria-hidden>
                <svg viewBox="0 0 24 24" focusable="false" aria-hidden="true">
                  <path
                    fill="currentColor"
                    d="M5 16.5V18H19v-1.5H5Zm0-3.75 3-3 3 3 6-6 1.06 1.06L11 14.81 8 11.81l-1.94 1.94L5 12.75Z"
                  />
                </svg>
              </span>
              Top 5 SKU Performance
            </h3>
          </div>
          <div className="table-wrap">
            <table className="table table-center">
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Category</th>
                  <th>Units</th>
                  <th>Revenue</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td className="fw-bold">Echelon Watch</td>
                  <td className="muted">Accessories</td>
                  <td className="fw-semibold">12.4k</td>
                  <td className="roi">$890k</td>
                </tr>
                <tr>
                  <td className="fw-bold">Velocity Shoe</td>
                  <td className="muted">Footwear</td>
                  <td className="fw-semibold">8.1k</td>
                  <td className="roi">$640k</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section className="card span-12 span-lg-6">
          <div className="card-head card-head-soft">
            <h3 className="caps-primary">
              <span className="caps-icon" aria-hidden>
                <svg viewBox="0 0 24 24" focusable="false" aria-hidden="true">
                  <path
                    fill="currentColor"
                    d="M5 16.5V18H19v-1.5H5Zm0-3.75 3-3 3 3 6-6 1.06 1.06L11 14.81 8 11.81l-1.94 1.94L5 12.75Z"
                  />
                </svg>
              </span>
              Market Share by Category
            </h3>
          </div>
          <div className="card-body">
            <div className="mini-cards">
              <div className="mini-card">
                <div>
                  <p className="mini-title">Footwear</p>
                  <p className="mini-sub">1,420 Active Products</p>
                </div>
                <div className="mini-right">
                  <p className="mini-value">98.2 Index</p>
                  <div className="spark" aria-hidden>
                    <span className="spark-on" />
                    <span className="spark-on" />
                    <span className="spark-on" />
                    <span className="spark-on" />
                    <span className="spark-on" />
                  </div>
                </div>
              </div>
              <div className="mini-card mini-card-muted">
                <div>
                  <p className="mini-title">Electronics</p>
                  <p className="mini-sub">890 Active Products</p>
                </div>
                <div className="mini-right">
                  <p className="mini-value">94.5 Index</p>
                  <div className="spark" aria-hidden>
                    <span className="spark-on" />
                    <span className="spark-on" />
                    <span className="spark-on" />
                    <span className="spark-on" />
                    <span className="spark-off" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="card span-12 span-lg-6">
          <div className="card-head card-head-soft">
            <h3 className="caps-primary">
              <span className="caps-icon" aria-hidden>
                <svg viewBox="0 0 24 24" focusable="false" aria-hidden="true">
                  <path
                    fill="currentColor"
                    d="M5 16.5V18H19v-1.5H5Zm0-3.75 3-3 3 3 6-6 1.06 1.06L11 14.81 8 11.81l-1.94 1.94L5 12.75Z"
                  />
                </svg>
              </span>
              Strategic Brand Exposure
            </h3>
          </div>
          <div className="table-wrap">
            <table className="table table-center">
              <thead>
                <tr>
                  <th>Brand</th>
                  <th>Products</th>
                  <th>Share</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td className="fw-bold">LuxeCore Systems</td>
                  <td>412</td>
                  <td className="roi">24.5%</td>
                </tr>
                <tr>
                  <td className="fw-bold">Vanguard Athletic</td>
                  <td>389</td>
                  <td className="roi">18.2%</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </main>
  );
}

