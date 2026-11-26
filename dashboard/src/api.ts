export type JobStatus =
  | 'Scheduled'
  | 'Pending'
  | 'Running'
  | 'Retrying'
  | 'Completed'
  | 'Dead'
  | 'Cancelled';

export interface Job {
  jobId: string;
  type: string;
  payload: unknown;
  priority: number;
  status: JobStatus;
  attemptCount: number;
  maxAttempts: number;
  lastError: string | null;
  createdAt: string;
  completedAt: string | null;
  runAt: string;
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface JobAttempt {
  attemptNumber: number;
  workerId: string | null;
  workerHostname: string | null;
  startedAt: string;
  finishedAt: string | null;
  result: string | null;
  error: string | null;
}

export interface Worker {
  id: string;
  hostname: string;
  status: string;
  concurrency: number;
  startedAt: string;
  lastHeartbeat: string;
  runningJobs: number;
}

export interface Stats {
  jobsByStatus: Record<string, number>;
  completedLastHour: number;
  attemptFailureRatePercent: number;
  completedPerMinute: { minute: string; count: number }[];
}

async function get<T>(path: string): Promise<T> {
  const res = await fetch(path);
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`${res.status} ${res.statusText}: ${body}`);
  }
  return res.json() as Promise<T>;
}

export const api = {
  jobs: (params: { status?: string; type?: string; priority?: string; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams();
    if (params.status) q.set('status', params.status);
    if (params.type) q.set('type', params.type);
    if (params.priority) q.set('priority', params.priority);
    q.set('page', String(params.page ?? 1));
    q.set('pageSize', String(params.pageSize ?? 15));
    return get<Paged<Job>>(`/api/jobs?${q.toString()}`);
  },
  attempts: (jobId: string) => get<JobAttempt[]>(`/api/jobs/${jobId}/attempts`),
  workers: () => get<Worker[]>('/api/workers'),
  stats: () => get<Stats>('/api/stats'),
};
