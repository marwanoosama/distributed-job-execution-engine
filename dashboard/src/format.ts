import type { JobStatus } from './api';

export function timeAgo(iso: string | null): string {
  if (!iso) return '—';
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 0) return `in ${formatFuture(-seconds)}`;
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ${minutes % 60}m ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

function formatFuture(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`;
  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

export function formatTime(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleTimeString();
}

export function priorityLabel(p: number): string {
  return p === 0 ? 'High' : p === 2 ? 'Low' : 'Normal';
}

export const STATUS_CLASS: Record<string, string> = {
  Scheduled: 'st-scheduled',
  Pending: 'st-pending',
  Running: 'st-running',
  Retrying: 'st-retrying',
  Completed: 'st-completed',
  Dead: 'st-dead',
  Cancelled: 'st-cancelled',
};

export function statusClass(s: string): string {
  return STATUS_CLASS[s] ?? 'st-pending';
}

export function isJobStatus(s: string): s is JobStatus {
  return s in STATUS_CLASS;
}

export function shortId(id: string): string {
  return id.slice(0, 8);
}
