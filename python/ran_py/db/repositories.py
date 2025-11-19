"""Repository pattern implementation matching .NET interfaces.

Repositories provide abstraction over data access, matching .NET ITaskRepository,
IEventRepository, and IReportRepository interfaces.
"""
from datetime import datetime
from typing import Protocol
from uuid import UUID

from sqlalchemy import and_, desc, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from ran_py.db.models import TaskEntity, TaskEventEntity, TaskReportEntity
from ran_py.logging_config import get_logger
from ran_py.models import ResearchTask

logger = get_logger(__name__)


class ITaskRepository(Protocol):
    """Task repository interface matching .NET ITaskRepository."""

    async def upsert_task_snapshot(self, task: ResearchTask) -> None:
        """Upsert task snapshot to database."""
        ...

    async def get(self, task_id: UUID) -> TaskEntity | None:
        """Get task by ID."""
        ...

    async def get_children_ids(self, parent_id: UUID) -> list[UUID]:
        """Get child task IDs for a parent task."""
        ...

    async def query_tasks(
        self,
        status: str | None = None,
        search_term: str | None = None,
        from_utc: datetime | None = None,
        to_utc: datetime | None = None,
        skip: int = 0,
        take: int = 100,
    ) -> list[TaskEntity]:
        """Query tasks with filters."""
        ...

    async def get_root_tasks(self, skip: int = 0, take: int = 100) -> list[TaskEntity]:
        """Get root tasks (tasks without parents)."""
        ...


class IEventRepository(Protocol):
    """Event repository interface matching .NET IEventRepository."""

    async def add_event(self, event: TaskEventEntity) -> None:
        """Add a task event."""
        ...

    async def get_events(self, task_ids: list[UUID], skip: int = 0, take: int = 100) -> list[TaskEventEntity]:
        """Get events for specific tasks."""
        ...

    async def query_events(
        self,
        task_id: UUID | None = None,
        from_utc: datetime | None = None,
        to_utc: datetime | None = None,
        skip: int = 0,
        take: int = 100,
    ) -> list[TaskEventEntity]:
        """Query events with filters."""
        ...


class IReportRepository(Protocol):
    """Report repository interface matching .NET IReportRepository."""

    async def upsert_report(self, task_id: UUID, report_markdown: str, generated_at_utc: datetime) -> None:
        """Upsert task report."""
        ...

    async def get(self, task_id: UUID) -> TaskReportEntity | None:
        """Get report by task ID."""
        ...


class TaskRepository:
    """Task repository implementation."""

    def __init__(self, session: AsyncSession):
        """Initialize repository with database session.

        Args:
            session: SQLAlchemy async session
        """
        self.session = session

    async def upsert_task_snapshot(self, task: ResearchTask) -> None:
        """Upsert task snapshot to database.

        Args:
            task: Research task to persist
        """
        # Check if task exists
        result = await self.session.execute(select(TaskEntity).filter_by(Id=task.id))
        existing = result.scalar_one_or_none()

        if existing:
            # Update existing task
            existing.Description = task.description
            existing.Priority = task.priority
            # TaskStatus enum uses use_enum_values=True, so it's already a string
            existing.Status = task.status if isinstance(task.status, str) else task.status.value
            existing.UpdatedAtUtc = datetime.utcnow()
            existing.ParentTaskId = task.parent_task_id
        else:
            # Create new task
            new_task = TaskEntity(
                Id=task.id,
                Description=task.description,
                Priority=task.priority,
                # TaskStatus enum uses use_enum_values=True, so it's already a string
                Status=task.status if isinstance(task.status, str) else task.status.value,
                CreatedAtUtc=task.created_at,
                UpdatedAtUtc=None,
                ParentTaskId=task.parent_task_id,
            )
            self.session.add(new_task)

        await self.session.flush()
        logger.debug(f"Upserted task snapshot: {task.id}")

    async def get(self, task_id: UUID) -> TaskEntity | None:
        """Get task by ID.

        Args:
            task_id: Task ID

        Returns:
            Task entity or None if not found
        """
        result = await self.session.execute(select(TaskEntity).filter_by(Id=task_id))
        return result.scalar_one_or_none()

    async def get_children_ids(self, parent_id: UUID) -> list[UUID]:
        """Get child task IDs for a parent task.

        Args:
            parent_id: Parent task ID

        Returns:
            List of child task IDs
        """
        result = await self.session.execute(select(TaskEntity.Id).filter_by(ParentTaskId=parent_id))
        return [row[0] for row in result.fetchall()]

    async def query_tasks(
        self,
        status: str | None = None,
        search_term: str | None = None,
        from_utc: datetime | None = None,
        to_utc: datetime | None = None,
        skip: int = 0,
        take: int = 100,
    ) -> list[TaskEntity]:
        """Query tasks with filters.

        Args:
            status: Filter by status
            search_term: Search in description
            from_utc: Filter by created date >= from_utc
            to_utc: Filter by created date <= to_utc
            skip: Number of records to skip
            take: Number of records to take

        Returns:
            List of matching task entities
        """
        query = select(TaskEntity)

        # Apply filters
        conditions = []
        if status:
            conditions.append(TaskEntity.Status == status)
        if search_term:
            conditions.append(TaskEntity.Description.contains(search_term))
        if from_utc:
            conditions.append(TaskEntity.CreatedAtUtc >= from_utc)
        if to_utc:
            conditions.append(TaskEntity.CreatedAtUtc <= to_utc)

        if conditions:
            query = query.where(and_(*conditions))

        # Order and paginate
        query = query.order_by(desc(TaskEntity.CreatedAtUtc)).offset(skip).limit(take)

        result = await self.session.execute(query)
        return list(result.scalars().all())

    async def get_root_tasks(self, skip: int = 0, take: int = 100) -> list[TaskEntity]:
        """Get root tasks (tasks without parents).

        Args:
            skip: Number of records to skip
            take: Number of records to take

        Returns:
            List of root task entities
        """
        query = (
            select(TaskEntity)
            .where(TaskEntity.ParentTaskId.is_(None))
            .order_by(desc(TaskEntity.CreatedAtUtc))
            .offset(skip)
            .limit(take)
        )

        result = await self.session.execute(query)
        return list(result.scalars().all())


class EventRepository:
    """Event repository implementation."""

    def __init__(self, session: AsyncSession):
        """Initialize repository with database session.

        Args:
            session: SQLAlchemy async session
        """
        self.session = session

    async def add_event(self, event: TaskEventEntity) -> None:
        """Add a task event.

        Args:
            event: Task event to persist
        """
        self.session.add(event)
        await self.session.flush()
        logger.debug(f"Added event: {event.EventType} for task {event.TaskId}")

    async def get_events(self, task_ids: list[UUID], skip: int = 0, take: int = 100) -> list[TaskEventEntity]:
        """Get events for specific tasks.

        Args:
            task_ids: List of task IDs
            skip: Number of records to skip
            take: Number of records to take

        Returns:
            List of task event entities
        """
        query = (
            select(TaskEventEntity)
            .where(TaskEventEntity.TaskId.in_(task_ids))
            .order_by(desc(TaskEventEntity.TimestampUtc))
            .offset(skip)
            .limit(take)
        )

        result = await self.session.execute(query)
        return list(result.scalars().all())

    async def query_events(
        self,
        task_id: UUID | None = None,
        from_utc: datetime | None = None,
        to_utc: datetime | None = None,
        skip: int = 0,
        take: int = 100,
    ) -> list[TaskEventEntity]:
        """Query events with filters.

        Args:
            task_id: Filter by task ID
            from_utc: Filter by timestamp >= from_utc
            to_utc: Filter by timestamp <= to_utc
            skip: Number of records to skip
            take: Number of records to take

        Returns:
            List of matching event entities
        """
        query = select(TaskEventEntity)

        # Apply filters
        conditions = []
        if task_id:
            conditions.append(TaskEventEntity.TaskId == task_id)
        if from_utc:
            conditions.append(TaskEventEntity.TimestampUtc >= from_utc)
        if to_utc:
            conditions.append(TaskEventEntity.TimestampUtc <= to_utc)

        if conditions:
            query = query.where(and_(*conditions))

        # Order and paginate
        query = query.order_by(desc(TaskEventEntity.TimestampUtc)).offset(skip).limit(take)

        result = await self.session.execute(query)
        return list(result.scalars().all())


class ReportRepository:
    """Report repository implementation."""

    def __init__(self, session: AsyncSession):
        """Initialize repository with database session.

        Args:
            session: SQLAlchemy async session
        """
        self.session = session

    async def upsert_report(self, task_id: UUID, report_markdown: str, generated_at_utc: datetime) -> None:
        """Upsert task report.

        Args:
            task_id: Task ID
            report_markdown: Report content in markdown format
            generated_at_utc: Report generation timestamp
        """
        # Check if report exists
        result = await self.session.execute(select(TaskReportEntity).filter_by(TaskId=task_id))
        existing = result.scalar_one_or_none()

        if existing:
            # Update existing report
            existing.ReportMarkdown = report_markdown
            existing.GeneratedAtUtc = generated_at_utc
        else:
            # Create new report
            new_report = TaskReportEntity(
                TaskId=task_id,
                ReportMarkdown=report_markdown,
                GeneratedAtUtc=generated_at_utc,
            )
            self.session.add(new_report)

        await self.session.flush()
        logger.debug(f"Upserted report for task: {task_id}")

    async def get(self, task_id: UUID) -> TaskReportEntity | None:
        """Get report by task ID.

        Args:
            task_id: Task ID

        Returns:
            Report entity or None if not found
        """
        result = await self.session.execute(select(TaskReportEntity).filter_by(TaskId=task_id))
        return result.scalar_one_or_none()
