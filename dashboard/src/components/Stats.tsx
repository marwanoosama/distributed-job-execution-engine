import type { Stats } from '../api';

const STATUS_ORDER = ['Scheduled', 'Pending', 'Running', 'Retrying', 'Completed', 'Dead', 'Cancelled'];
const STATUS_COLORS: Record<string, string> = {
  Scheduled: '#a78bfa',
  Pending: '#60a5fa',
  Running: '#38bdf8',
  Retrying: '#fbbf24',
  Completed: '#34d399',
  Dead: '#f87171',
  Cancelled: '#94a3b8',
};

export function StatsCards({ stats }: { stats: Stats }) {
  return (
    <div className="stats-row">
      {STATUS_ORDER.map((s) => (
        <div className="stat-card" key={s}>
          <div className="stat-value" style={{ color: STATUS_COLORS[s] }}>
            {stats.jobsByStatus[s] ?? 0}
          </div>
          <div className="stat-label">{s}</div>
        </div>
      ))}
      <div className="stat-card">
        <div className="stat-value" style={{ color: '#e2e8f0' }}>{stats.completedLastHour}</div>
        <div className="stat-label">Completed (1h)</div>
      </div>
      <div className="stat-card">
        <div className="stat-value" style={{ color: stats.attemptFailureRatePercent > 10 ? '#f87171' : '#34d399' }}>
          {stats.attemptFailureRatePercent}%
        </div>
        <div className="stat-label">Failure rate (1h)</div>
      </div>
    </div>
  );
}

export function ThroughputChart({ stats }: { stats: Stats }) {
  const points = stats.completedPerMinute;
  const max = Math.max(1, ...points.map((p) => p.count));
  const barW = 100 / points.length;

  return (
    <div className="chart-card">
      <div className="chart-title">Completed per minute (last hour)</div>
      <svg viewBox="0 0 100 42" preserveAspectRatio="none" className="chart-svg">
        {points.map((p, i) => {
          const h = (p.count / max) * 36;
          return (
            <g key={p.minute}>
              <rect
                x={i * barW + barW * 0.15}
                y={38 - h}
                width={barW * 0.7}
                height={h}
                rx="0.5"
                fill={p.count > 0 ? '#34d399' : '#1e293b'}
              />
              {p.count > 0 && (
                <text x={i * barW + barW / 2} y={35 - h} fill="#94a3b8" fontSize="2.4" textAnchor="middle">
                  {p.count}
                </text>
              )}
            </g>
          );
        })}
      </svg>
      <div className="chart-axis">
        <span>{new Date(points[0]?.minute ?? Date.now()).toLocaleTimeString()}</span>
        <span>now</span>
      </div>
    </div>
  );
}
