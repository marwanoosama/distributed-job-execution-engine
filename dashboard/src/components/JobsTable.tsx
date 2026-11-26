import { Fragment, useState } from 'react';
import type { Job, JobAttempt, Paged } from '../api';
import { statusClass, priorityLabel, timeAgo, formatTime, isJobStatus } from '../format';

interface Props {
  data: Paged<Job> | null;
  page: number;
  pageSize: number;
  filters: { status: string; type: string; priority: string };
  onPageChange: (page: number) => void;
  onFilterChange: (f: { status: string; type: string; priority: string }) => void;
}

export function JobsTable({ data, page, pageSize, filters, onPageChange, onFilterChange }: Props) {
  const [openJob, setOpenJob] = useState<string | null>(null);
  const totalPages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1;

  return (
    <section className="panel">
      <div className="panel-header">
        <h2>Jobs <span className="count-badge">{data?.total ?? '…'}</span></h2>
        <div className="filters">
          <select value={filters.status} onChange={(e) => { onFilterChange({ ...filters, status: e.target.value }); onPageChange(1); }}>
            <option value="">All statuses</option>
            {Object.keys(statusLabels).map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <select value={filters.priority} onChange={(e) => { onFilterChange({ ...filters, priority: e.target.value }); onPageChange(1); }}>
            <option value="">All priorities</option>
            <option value="0">High</option>
            <option value="1">Normal</option>
            <option value="2">Low</option>
          </select>
          <input
            placeholder="Filter by type…"
            value={filters.type}
            onChange={(e) => { onFilterChange({ ...filters, type: e.target.value }); }}
            onKeyDown={(e) => { if (e.key === 'Enter') onPageChange(1); }}
          />
        </div>
      </div>

      <table>
        <thead>
          <tr>
            <th>Job</th>
            <th>Type</th>
            <th>Status</th>
            <th>Priority</th>
            <th>Attempts</th>
            <th>Created</th>
            <th>Last error</th>
          </tr>
        </thead>
        <tbody>
          {data?.items.map((job) => (
            <Fragment key={job.jobId}>
              <tr
                className={`job-row ${openJob === job.jobId ? 'open' : ''}`}
                onClick={() => setOpenJob(openJob === job.jobId ? null : job.jobId)}
              >
                <td className="mono">{job.jobId.slice(0, 8)}…</td>
                <td className="mono">{job.type}</td>
                <td><span className={`pill ${statusClass(job.status)}`}>{job.status}</span></td>
                <td>{priorityLabel(job.priority)}</td>
                <td>{job.attemptCount}/{job.maxAttempts}</td>
                <td title={job.createdAt}>{timeAgo(job.createdAt)}</td>
                <td className="error-cell" title={job.lastError ?? ''}>{job.lastError ?? '—'}</td>
              </tr>
              {openJob === job.jobId && (
                <tr className="details-row">
                  <td colSpan={7}>
                    <AttemptsPanel jobId={job.jobId} />
                  </td>
                </tr>
              )}
            </Fragment>
          ))}
          {data && data.items.length === 0 && (
            <tr><td colSpan={7} className="empty">No jobs match the filters.</td></tr>
          )}
        </tbody>
      </table>

      <div className="pagination">
        <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>← Prev</button>
        <span>Page {page} of {totalPages}</span>
        <button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>Next →</button>
      </div>
    </section>
  );
}

const statusLabels: Record<string, string> = {
  Scheduled: 'Scheduled', Pending: 'Pending', Running: 'Running', Retrying: 'Retrying',
  Completed: 'Completed', Dead: 'Dead', Cancelled: 'Cancelled',
};

function AttemptsPanel({ jobId }: { jobId: string }) {
  const [attempts, setAttempts] = useState<JobAttempt[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (attempts === null && error === null) {
    fetch(`/api/jobs/${jobId}/attempts`)
      .then((r) => { if (!r.ok) throw new Error(String(r.status)); return r.json(); })
      .then(setAttempts)
      .catch((e) => setError(String(e)));
  }

  if (error) return <div className="attempts">Failed to load attempts: {error}</div>;
  if (!attempts) return <div className="attempts">Loading attempts…</div>;
  if (attempts.length === 0) return <div className="attempts">No attempts recorded.</div>;

  return (
    <div className="attempts">
      <table>
        <thead>
          <tr><th>#</th><th>Worker</th><th>Result</th><th>Started</th><th>Finished</th><th>Error</th></tr>
        </thead>
        <tbody>
          {attempts.map((a, i) => (
            <tr key={i}>
              <td>{a.attemptNumber}</td>
              <td className="mono">{a.workerHostname ?? a.workerId?.slice(0, 8) ?? '—'}</td>
              <td><span className={`pill ${a.result ? statusClass(resultToStatus(a.result)) : ''}`}>{a.result ?? '…'}</span></td>
              <td>{formatTime(a.startedAt)}</td>
              <td>{a.finishedAt ? formatTime(a.finishedAt) : 'running…'}</td>
              <td className="error-cell">{a.error ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function resultToStatus(result: string): string {
  return isJobStatus(result) ? result : result === 'Success' ? 'Completed' : 'Dead';
}
