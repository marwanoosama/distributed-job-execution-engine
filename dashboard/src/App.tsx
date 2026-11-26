import { useCallback, useEffect, useRef, useState } from 'react';
import { api, type Job, type Paged, type Stats, type Worker } from './api';
import { StatsCards, ThroughputChart } from './components/Stats';
import { JobsTable } from './components/JobsTable';
import { WorkersTable } from './components/WorkersTable';

const REFRESH_MS = 5000;

export default function App() {
  const [stats, setStats] = useState<Stats | null>(null);
  const [workers, setWorkers] = useState<Worker[] | null>(null);
  const [jobs, setJobs] = useState<Paged<Job> | null>(null);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState({ status: '', type: '', priority: '' });
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const pageSize = 15;

  const filtersRef = useRef(filters);
  const pageRef = useRef(page);
  filtersRef.current = filters;
  pageRef.current = page;

  const refresh = useCallback(async () => {
    try {
      const [s, w, j] = await Promise.all([
        api.stats(),
        api.workers(),
        api.jobs({
          status: filtersRef.current.status || undefined,
          type: filtersRef.current.type || undefined,
          priority: filtersRef.current.priority || undefined,
          page: pageRef.current,
          pageSize,
        }),
      ]);
      setStats(s);
      setWorkers(w);
      setJobs(j);
      setLastUpdated(new Date());
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }, []);

  useEffect(() => {
    refresh();
    const t = setInterval(refresh, REFRESH_MS);
    return () => clearInterval(t);
  }, [refresh]);

  // refetch jobs immediately when page or filters change
  useEffect(() => {
    api.jobs({
      status: filters.status || undefined,
      type: filters.type || undefined,
      priority: filters.priority || undefined,
      page,
      pageSize,
    }).then(setJobs).catch(() => {});
  }, [filters, page]);

  return (
    <div className="app">
      <header>
        <h1>Job Engine</h1>
        <div className="header-right">
          {error && <span className="api-error">API error: {error}</span>}
          {lastUpdated && <span className="subtle">refreshed {lastUpdated.toLocaleTimeString()} · every {REFRESH_MS / 1000}s</span>}
        </div>
      </header>

      {stats && <StatsCards stats={stats} />}
      {stats && <ThroughputChart stats={stats} />}

      <JobsTable
        data={jobs}
        page={page}
        pageSize={pageSize}
        filters={filters}
        onPageChange={setPage}
        onFilterChange={setFilters}
      />

      <WorkersTable workers={workers} />
    </div>
  );
}
