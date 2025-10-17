"""Events API routes for SSE streaming matching .NET endpoints."""
import asyncio
import json
from datetime import datetime
from typing import AsyncGenerator
from uuid import UUID

from fastapi import APIRouter, Depends
from fastapi.responses import StreamingResponse
from sqlalchemy.ext.asyncio import AsyncSession

from ran_py.db.repositories import EventRepository
from ran_py.db.session import get_async_db
from ran_py.logging_config import get_logger

logger = get_logger(__name__)

router = APIRouter()


async def event_stream() -> AsyncGenerator[str, None]:
    """Generate Server-Sent Events stream.

    Matches .NET endpoint: GET /api/events

    This sends real-time updates to connected clients.
    In a production system, this would integrate with a pub/sub system
    or message queue for scalability.

    Yields:
        SSE-formatted messages
    """
    # Send initial connection message
    initial_message = {
        "type": "connected",
        "timestamp": datetime.utcnow().isoformat(),
        "message": "Connected to Research Agent Network Python service",
    }
    yield f"data: {json.dumps(initial_message)}\n\n"

    # Heartbeat loop
    try:
        while True:
            # In production, this would listen to a message queue
            # For now, send periodic heartbeats
            await asyncio.sleep(20)

            heartbeat = {
                "type": "heartbeat",
                "timestamp": datetime.utcnow().isoformat(),
            }
            yield f"data: {json.dumps(heartbeat)}\n\n"

    except asyncio.CancelledError:
        logger.info("SSE client disconnected")
        raise


@router.get("/events")
async def stream_events():
    """Stream task events via Server-Sent Events.

    Matches .NET endpoint: GET /api/events

    Returns:
        Streaming response with SSE content type
    """
    logger.info("SSE client connected")

    return StreamingResponse(
        event_stream(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
        },
    )


@router.get("/tasks/{task_id}/events")
async def get_task_events(
    task_id: UUID,
    include_children: bool = False,
    skip: int = 0,
    take: int = 500,
    db: AsyncSession = Depends(get_async_db),
):
    """Get events for a specific task.

    Matches .NET endpoint: GET /api/tasks/{id}/events

    Args:
        task_id: Task ID
        include_children: Include events from child tasks
        skip: Number of records to skip
        take: Number of records to take
        db: Database session

    Returns:
        List of task events
    """
    from ran_py.db.repositories import TaskRepository

    repo = EventRepository(db)
    task_repo = TaskRepository(db)

    # Collect task IDs (task + children if requested)
    task_ids = [task_id]

    if include_children:
        # BFS to collect all descendant task IDs
        queue = [task_id]
        visited = {task_id}

        while queue:
            current = queue.pop(0)
            children = await task_repo.get_children_ids(current)

            for child_id in children:
                if child_id not in visited:
                    visited.add(child_id)
                    task_ids.append(child_id)
                    queue.append(child_id)

    # Get events for all collected task IDs
    events = await repo.get_events(
        task_ids=task_ids,
        skip=skip,
        take=min(take, 5000),  # Cap at 5000
    )

    return {
        "taskId": str(task_id),
        "includeChildren": include_children,
        "events": [
            {
                "id": str(e.Id),
                "taskId": str(e.TaskId),
                "eventType": e.EventType,
                "status": e.Status,
                "message": e.Message,
                "timestampUtc": e.TimestampUtc.isoformat(),
                "agentRole": e.AgentRole,
            }
            for e in events
        ],
        "count": len(events),
    }
