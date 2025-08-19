# Reports API

## Endpoints

- GET `/api/reports/{taskId}`
  - Returns: `{ taskId, markdown, generatedAtUtc }`
  - 404 if not found

- GET `/api/reports/{taskId}/download?format=md`
  - Returns: `text/markdown` file attachment `report-{taskId}.md`
  - Other formats are not supported yet

## Notes

- Reports are upserted automatically when task `completed`/`failed` events are published.
- You can force persistence via `POST /api/tasks/{id}/report`.

## How to Test

1. Submit a task and wait for completion and finalization
2. GET `/api/reports/{id}` should return JSON with `markdown`
3. GET `/api/reports/{id}/download?format=md` should trigger a markdown download

