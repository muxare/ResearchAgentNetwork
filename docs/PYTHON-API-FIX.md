# Python API FastAPI Dependency Fix

## Issue

The Python backend API was returning 500 errors:
```
'_AsyncGeneratorContextManager' object has no attribute 'execute'
```

## Root Cause

The `get_async_db()` function in [python/ran_py/db/session.py](python/ran_py/db/session.py) was decorated with `@asynccontextmanager`, which returns an `AsyncGeneratorContextManager` object. However, FastAPI's `Depends()` expects a plain async generator function that yields a session.

When FastAPI tried to inject the database session, it received a context manager instead of a session object, causing the `execute` method to fail.

## Solution

**Split the function into two variants:**

1. **`get_async_db_context()`** - Decorated with `@asynccontextmanager` for manual usage:
   ```python
   @asynccontextmanager
   async def get_async_db_context() -> AsyncGenerator[AsyncSession, None]:
       """For manual async with usage."""
       async with AsyncSessionLocal() as session:
           try:
               yield session
               await session.commit()
           except Exception:
               await session.rollback()
               raise
           finally:
               await session.close()
   ```

2. **`get_async_db()`** - Plain async generator for FastAPI dependency injection:
   ```python
   async def get_async_db() -> AsyncGenerator[AsyncSession, None]:
       """For FastAPI Depends() injection."""
       async with AsyncSessionLocal() as session:
           try:
               yield session
               await session.commit()
           except Exception:
               await session.rollback()
               raise
           finally:
               await session.close()
   ```

## Files Changed

- [python/ran_py/db/session.py](python/ran_py/db/session.py:86-128) - Split `get_async_db()` into two functions

## Verification

**API Endpoints Now Working:**

```bash
# List tasks (empty initially)
$ curl http://localhost:8090/api/tasks
{"tasks":[],"count":0}

# Submit task
$ curl -X POST http://localhost:8090/api/tasks \
  -H "Content-Type: application/json" \
  -d '{"description":"Explain quantum computing","priority":5}'
{"id":"0fc22d91-141d-40b0-92cd-68fe223906b6"}

# Get task by ID
$ curl http://localhost:8090/api/tasks/0fc22d91-141d-40b0-92cd-68fe223906b6
{
  "id":"0fc22d91-141d-40b0-92cd-68fe223906b6",
  "description":"Explain quantum computing",
  "status":"Pending",
  "priority":5,
  "createdAtUtc":"2025-10-20T14:39:48.542878",
  "updatedAtUtc":null,
  "parentTaskId":null
}

# SSE events stream
$ curl -N http://localhost:8090/api/events
data: {"type":"connected","timestamp":"2025-10-20T14:39:35.754135",...}
```

**Unit Tests Still Passing:**
```
14 passed, 33 warnings in 1.86s
Coverage: 96% for db.repositories, 96% for db.models
```

## Impact

- ✅ All API endpoints functional
- ✅ Frontend can now connect to Python backend
- ✅ SSE streaming working
- ✅ Database persistence working
- ✅ All unit tests passing

## Related

This fix enables Phase 2 to be fully functional with live API testing. The Python backend can now be tested end-to-end alongside the .NET backend using the Docker profile system.
