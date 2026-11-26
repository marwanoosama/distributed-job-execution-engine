# Distributed Job Engine

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=.net&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-4169E1?logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white)
![license](https://img.shields.io/badge/license-MIT-green)

> A **distributed background job engine** built from scratch in .NET 10, PostgreSQL, and Redis. Designed to show how real job systems work under the hood — exponential backoff with jitter, dead-letter queue, idempotency keys, priority queues, scheduled jobs, per-job timeouts, distributed leases, crash recovery, and a live React dashboard.

## Table of Contents

- [Architecture](#architecture)
- [Features](#features)
- [Example Usage (curl)](#example-usage-curl)
- [Running it](#running-it)
- [Test cases](#test-cases)
- [Design decisions](#design-decisions)
- [License](#license)

## Architecture

```
┌─────────┐     ┌────────────┐     ┌────────────┐
│   API   │────▶│ PostgreSQL │◀────│  Scheduler │
│ (jobs)  │     │  (source   │     │ (reconciler│
└────┬────┘     │   of truth)│     │  + crash   │
     │          └────────────┘     │  monitor)  │
     │                                   │
     ▼                                   ▼
┌─────────────────────────────────────────────┐
│              Redis                          │
│  queues · delayed set · dlq · leases        │
└────────────────────┬────────────────────────┘
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
   ┌────────┐   ┌────────┐   ┌────────┐
   │Worker 1│   │Worker 2│   │Worker 3│
   └────────┘   └────────┘   └────────┘
```

| Component | Responsibility |
|---|---|
| **API** | Accepts job submissions, validates, writes to Postgres, enqueues in Redis |
| **Worker(s)** | Pull job ids from Redis priority queues, claim the job in Postgres, run the handler, write the result |
| **Scheduler** | DelayedJobMover promotes due scheduled jobs (1s), Reconciler re-enqueues stuck jobs (30s), CrashMonitor recovers expired leases (5s) |
| **PostgreSQL** | Source of truth. Every status change is a conditional update |
| **Redis** | Priority queues, delayed sorted set, dead-letter list, distributed leases |

## Features

### Job lifecycle
Jobs move `Pending → Running → Completed / Retrying / Dead`. Every transition is a CAS update (`WHERE status = ... AND lease_token = ...`), so two workers can never both transition the same job.

### Retries with exponential backoff
Failed attempts retry on `5 × 2^(attempt-1)` seconds, capped at 300s, plus 0–3s random jitter. The jitter stops simultaneous failures from retrying in lockstep and overloading a recovering system.

### Dead-letter queue
A job that exhausts its attempts goes to a `dlq` list in Redis and stays queryable in Postgres with its last error. A retry endpoint re-queues it from attempt 1.

### Idempotency
Duplicate `idempotencyKey` submissions return the existing job. Enforced by a unique index, with a fallback that catches the unique-violation race between the existence check and the insert.

### Priority queues
Three Redis lists (high / normal / low). Workers always drain high before normal before low.

### Scheduled jobs
Submit with a future `runAt` and the job waits in a Redis sorted set (score = run time) until a 1-second loop promotes it. A `runAt` in the past runs immediately.

### Timeouts
Every attempt runs under a cancellation token with a timeout (default 600s). A hung handler is cancelled, the attempt is recorded as failed, and normal retry logic takes over.

### Graceful shutdown
Ctrl+C lets the in-flight job finish within a shutdown budget (default 30s), marks the worker `Stopped` in the DB, then exits. If the budget expires first, the job stays `Running` and crash recovery handles it.

### Leases + crash recovery
- Before running a job, the worker acquires `lease:{jobId}` in Redis (30s TTL) and renews it every 10s while the job runs.
- Renew and release are Lua scripts that compare the token first — only the current owner can act, so two workers can never both own a job.
- The CrashMonitor (5s tick) finds `Running` jobs whose lease expired, marks the attempt `LeaseExpired`, and requeues the job with backoff or sends it to Dead + DLQ.
- A worker that wakes up after losing its lease (SIGSTOP, GC pause) detects the loss on its next renew and discards its result instead of writing it.

### Heartbeats
Workers register in a `workers` table and ping every 5s. 15s of silence marks them `Dead`.

### Non-retryable failures
Handlers throw `NonRetryableException` for permanent failures (missing handler, invalid payload). The job goes `Dead` on attempt 1 instead of consuming retries.

### Dashboard
A React + Vite app polling the dashboard API every 5s: status counters, completions-per-minute chart, filterable job table with attempt history, and a live worker list.

## Example Usage (curl)

### Submit a job
```bash
curl -X POST localhost:5000/api/jobs -H "Content-Type: application/json" -d '{
  "type": "SendEmail",
  "payload": { "to": "user@example.com" },
  "priority": 1,
  "maxAttempts": 5,
  "idempotencyKey": "order-42-email",
  "runAt": "2026-12-01T10:00:00Z"
}'
```
Returns `{"jobId": "..."}`. All fields except `type` and `payload` are optional.

### Get job status
```bash
curl localhost:5000/api/jobs/{id}
```

### Retry a dead job
```bash
curl -X POST localhost:5000/api/jobs/{id}/retry
```

### Cancel a job
```bash
curl -X POST localhost:5000/api/jobs/{id}/cancel
```

### List jobs (filters + pagination)
```bash
curl "localhost:5000/api/jobs?status=Completed&type=SendEmail&page=1&pageSize=20"
```

### Get attempt history
```bash
curl localhost:5000/api/jobs/{id}/attempts
```

### List workers
```bash
curl localhost:5000/api/workers
```

### Get stats
```bash
curl localhost:5000/api/stats
```

## Running it

Requires .NET 10, PostgreSQL (database `JobEngine`), Redis, and Node 20+ for the dashboard.

```bash
# API — runs migrations on startup
dotnet run --project src/JobEngine.Api

# Scheduler
dotnet run --project src/JobEngine.Scheduler

# Workers (2-3 to see crash recovery in action)
dotnet run --project src/JobEngine.Worker

# Dashboard
cd dashboard && npm install && npm run dev   # → http://localhost:3000
```

## Test cases

All scenarios below were executed against the running system. Commands are reproducible.

### 1. Retry then Dead
```bash
curl -X POST localhost:5000/api/jobs -H "Content-Type: application/json" \
  -d '{"type":"AlwaysFail","payload":{},"maxAttempts":4}'
```
Attempts land at ~+5s, +10s, +20s, then `Dead`. `job_attempts` has 4 failed rows with growing gaps, and the job id is in `redis-cli -n 2 LRANGE dlq 0 -1`.

### 2. Idempotency
```bash
for i in $(seq 10); do
  curl -s -X POST localhost:5000/api/jobs -H "Content-Type: application/json" \
    -d '{"type":"SendEmail","payload":{},"idempotencyKey":"test-1"}' > /dev/null &
done
wait
# SELECT count(*) FROM jobs WHERE idempotency_key = 'test-1';  -- 1
```
All 10 responses return the same `jobId`; the DB has exactly 1 row.

### 3. Load test — FailRandomly × 500
```bash
for i in $(seq 500); do
  curl -s -X POST localhost:5000/api/jobs -H "Content-Type: application/json" \
    -d '{"type":"FailRandomly","payload":{}}' -o /dev/null &
done
wait
```
Result: 495/495 `Completed`, 0 `Dead` (expected ~1 at 0.3⁵ ≈ 0.24%), attempt-count distribution matches the binomial model for a 30% failure rate.

### 4. Retry endpoint
Retry a `Dead` job → `202`, runs again from attempt 1, attempt history keeps growing.

### 5. Redis wipe during retry wait
Delete all Redis data while a job waits to retry (`redis-cli FLUSHALL`). The Reconciler re-enqueues it from Postgres after ~60-90s. Result: downtime, not data loss.

### 6. Scheduled job
```bash
RUN_AT=$(date -u -d "+20 seconds" +%Y-%m-%dT%H:%M:%SZ)
curl -X POST localhost:5000/api/jobs -H "Content-Type: application/json" \
  -d "{\"type\":\"SendEmail\",\"payload\":{},\"runAt\":\"$RUN_AT\"}"
```
GET shows `Scheduled`, `redis-cli -n 2 ZRANGE delayed 0 -1 WITHSCORES` shows the entry, and the job fires within ~1s of the requested time. A `runAt` more than 1 year out is rejected with 400.

### 7. Priority
Queue 100 low-priority jobs, then 5 high-priority, then start one worker. The first 5 completions are all priority 0.

### 8. Cancel
Cancel a `Scheduled` job → `200`, and it never runs. Cancel `Running` or `Completed` → `409`. Cancel `Retrying` → `200`, frozen at the current attempt.

### 9. Timeout
Start the worker with `-- Worker:JobTimeoutSeconds=5`, submit a 15s job with `maxAttempts: 2`. Two attempts, both "Job timed out after 5s", then `Dead`.

### 10. Graceful shutdown
Ctrl+C mid-job: the job finishes, then the process exits. With a 60s job and a 10s shutdown budget, the process exits after 10s and the job stays `Running` for crash recovery.

### 11. Crash recovery
`kill -9` the worker that owns a `Sleep 60` job. Within ~30-40s: attempt 1 = `LeaseExpired`, the job is retried on a different worker and ends `Completed`. Repeated 10 times, works every time.

### 12. Zombie worker
SIGSTOP the owning worker mid-job → the lease expires and the job is recovered. SIGCONT the worker → it logs "lease lost, result discarded" and writes nothing; attempt history is unchanged.

### 13. Redis wipe mid-run
Submit 20 × `Sleep 30`, then `redis-cli -n 2 FLUSHDB`. All 20 finish; about half run twice (recovered + re-executed) — the expected cost of at-least-once delivery.

### 14. Leftovers
A `Running` job orphaned by a crash is recovered by the first CrashMonitor pass after startup.

### 15. Non-retryable
Submit a job type with no registered handler → `Dead` on attempt 1 with "No handler for job type '...'", no retries.


## Project structure

```
src/
├── JobEngine.Api/              # HTTP API (job submission, dashboard)
├── JobEngine.Application/      # Services, interfaces, records
├── JobEngine.Domain/           # Entities and enums
├── JobEngine.Infrastructure/   # EF Core, Redis, persistence
├── JobEngine.Scheduler/        # CrashMonitor, DelayedJobMover, Reconciler
├── JobEngine.Worker/           # Job execution, leases, heartbeats
└── JobEngine.SampleJobs/       # Demo handlers (Sleep, SendEmail, etc.)
dashboard/                      # React + Vite frontend
tests/                          # Integration tests
docs/                           # Architecture diagrams
```

## License

[MIT](LICENSE)
