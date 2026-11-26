import type { Worker } from '../api';
import { timeAgo } from '../format';

export function WorkersTable({ workers }: { workers: Worker[] | null }) {
  return (
    <section className="panel">
      <div className="panel-header">
        <h2>Workers <span className="count-badge">{workers?.length ?? '…'}</span></h2>
      </div>
      <table>
        <thead>
          <tr>
            <th>Worker</th>
            <th>Status</th>
            <th>Concurrency</th>
            <th>Running</th>
            <th>Last heartbeat</th>
          </tr>
        </thead>
        <tbody>
          {workers?.map((w) => (
            <tr key={w.id}>
              <td>
                <div className="mono">{w.id.slice(0, 8)}…</div>
                <div className="subtle">{w.hostname}</div>
              </td>
              <td>
                <span className={`pill ${w.status === 'Alive' ? 'st-completed' : w.status === 'Stopped' ? 'st-cancelled' : 'st-dead'}`}>
                  {w.status}
                </span>
              </td>
              <td>{w.concurrency} loops</td>
              <td>{w.runningJobs > 0 ? <span className="running-count">{w.runningJobs}</span> : '0'}</td>
              <td title={w.lastHeartbeat}>{timeAgo(w.lastHeartbeat)}</td>
            </tr>
          ))}
          {workers && workers.length === 0 && (
            <tr><td colSpan={5} className="empty">No workers registered yet.</td></tr>
          )}
        </tbody>
      </table>
    </section>
  );
}
