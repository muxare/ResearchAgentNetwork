"""Task API routes matching .NET endpoints."""
from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy.ext.asyncio import AsyncSession

from ran_py.db.repositories import TaskRepository
from ran_py.db.session import get_async_db
from ran_py.graphs.orchestrator import get_orchestrator
from ran_py.logging_config import get_logger
from ran_py.models import ResearchTask, TaskStatus

logger = get_logger(__name__)

router = APIRouter()


class TaskSubmitRequest(BaseModel):
    """Task submission request matching .NET TaskSubmit."""

    description: str = Field(description="Research task description")
    priority: int | None = Field(default=5, description="Task priority (1-10)")


class TaskSubmitResponse(BaseModel):
    """Task submission response."""

    id: UUID = Field(description="Task ID")


@router.post("/tasks", response_model=TaskSubmitResponse)
async def submit_task(
    request: TaskSubmitRequest,
    db: AsyncSession = Depends(get_async_db),
) -> TaskSubmitResponse:
    """Submit a new research task.

    Matches .NET endpoint: POST /api/tasks

    Args:
        request: Task submission request
        db: Database session

    Returns:
        Task submission response with ID
    """
    logger.info(f"Submitting task: {request.description[:50]}...")

    # Create task
    task = ResearchTask(
        description=request.description,
        priority=request.priority or 5,
        status=TaskStatus.PENDING,
    )

    # Persist task
    repo = TaskRepository(db)
    await repo.upsert_task_snapshot(task)

    # Submit to orchestrator (async, don't wait for completion)
    orchestrator = get_orchestrator()
    # Note: In production, this would be handled by a background worker
    # For now, we'll just persist and return the ID
    # The actual processing would be triggered separately

    logger.info(f"Task submitted with ID: {task.id}")

    return TaskSubmitResponse(id=task.id)


@router.get("/tasks/{task_id}")
async def get_task(
    task_id: UUID,
    db: AsyncSession = Depends(get_async_db),
):
    """Get task status by ID.

    Matches .NET endpoint: GET /api/tasks/{id}

    Args:
        task_id: Task ID
        db: Database session

    Returns:
        Task entity

    Raises:
        HTTPException: If task not found
    """
    repo = TaskRepository(db)
    task_entity = await repo.get(task_id)

    if not task_entity:
        logger.warning(f"Task not found: {task_id}")
        raise HTTPException(status_code=404, detail="Task not found")

    return {
        "id": str(task_entity.Id),
        "description": task_entity.Description,
        "status": task_entity.Status,
        "priority": task_entity.Priority,
        "createdAtUtc": task_entity.CreatedAtUtc.isoformat(),
        "updatedAtUtc": task_entity.UpdatedAtUtc.isoformat() if task_entity.UpdatedAtUtc else None,
        "parentTaskId": str(task_entity.ParentTaskId) if task_entity.ParentTaskId else None,
    }


@router.get("/tasks/{task_id}/children")
async def get_task_children(
    task_id: UUID,
    db: AsyncSession = Depends(get_async_db),
):
    """Get child tasks for a parent task.

    Matches .NET endpoint: GET /api/tasks/{id}/children

    Args:
        task_id: Parent task ID
        db: Database session

    Returns:
        List of child task IDs
    """
    repo = TaskRepository(db)
    children_ids = await repo.get_children_ids(task_id)

    return {"taskId": str(task_id), "childrenIds": [str(cid) for cid in children_ids]}


@router.get("/tasks")
async def list_tasks(
    skip: int = 0,
    take: int = 100,
    status: str | None = None,
    db: AsyncSession = Depends(get_async_db),
):
    """List tasks with optional filtering.

    Args:
        skip: Number of records to skip
        take: Number of records to take
        status: Filter by status
        db: Database session

    Returns:
        List of tasks
    """
    repo = TaskRepository(db)
    tasks = await repo.query_tasks(
        status=status,
        search_term=None,
        from_utc=None,
        to_utc=None,
        skip=skip,
        take=min(take, 1000),  # Cap at 1000
    )

    return {
        "tasks": [
            {
                "id": str(t.Id),
                "description": t.Description,
                "status": t.Status,
                "priority": t.Priority,
                "createdAtUtc": t.CreatedAtUtc.isoformat(),
                "parentTaskId": str(t.ParentTaskId) if t.ParentTaskId else None,
            }
            for t in tasks
        ],
        "count": len(tasks),
    }
