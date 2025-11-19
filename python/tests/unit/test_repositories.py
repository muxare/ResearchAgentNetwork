"""Unit tests for repository implementations."""
import pytest
from datetime import datetime
from uuid import uuid4

from sqlalchemy.ext.asyncio import AsyncSession, create_async_engine
from sqlalchemy.orm import sessionmaker

from ran_py.db.models import Base, TaskEntity, TaskEventEntity, TaskReportEntity
from ran_py.db.repositories import TaskRepository, EventRepository, ReportRepository
from ran_py.models import ResearchTask, TaskStatus


@pytest.fixture
async def async_session():
    """Create an in-memory async database session for testing."""
    # Create async engine for in-memory SQLite
    engine = create_async_engine("sqlite+aiosqlite:///:memory:", echo=False)

    # Create tables
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)

    # Create session factory
    async_session_factory = sessionmaker(
        engine, class_=AsyncSession, expire_on_commit=False
    )

    # Create and yield session
    async with async_session_factory() as session:
        yield session

    # Cleanup
    await engine.dispose()


class TestTaskRepository:
    """Tests for TaskRepository."""

    @pytest.mark.asyncio
    async def test_upsert_task_snapshot_create(self, async_session):
        """Test creating a new task."""
        repo = TaskRepository(async_session)

        task = ResearchTask(
            id=uuid4(),
            description="Test task",
            status=TaskStatus.PENDING,
            priority=5,
        )

        await repo.upsert_task_snapshot(task)
        await async_session.commit()

        # Verify task was created
        result = await repo.get(task.id)
        assert result is not None
        assert result.Description == "Test task"
        assert result.Priority == 5
        assert result.Status == "Pending"

    @pytest.mark.asyncio
    async def test_upsert_task_snapshot_update(self, async_session):
        """Test updating an existing task."""
        repo = TaskRepository(async_session)

        task = ResearchTask(
            id=uuid4(),
            description="Original description",
            status=TaskStatus.PENDING,
            priority=5,
        )

        # Create task
        await repo.upsert_task_snapshot(task)
        await async_session.commit()

        # Update task
        task.description = "Updated description"
        task.status = TaskStatus.COMPLETED
        await repo.upsert_task_snapshot(task)
        await async_session.commit()

        # Verify update
        result = await repo.get(task.id)
        assert result is not None
        assert result.Description == "Updated description"
        assert result.Status == "Completed"
        assert result.UpdatedAtUtc is not None

    @pytest.mark.asyncio
    async def test_get_nonexistent(self, async_session):
        """Test getting a task that doesn't exist."""
        repo = TaskRepository(async_session)

        result = await repo.get(uuid4())
        assert result is None

    @pytest.mark.asyncio
    async def test_get_children_ids(self, async_session):
        """Test getting child task IDs."""
        repo = TaskRepository(async_session)

        # Create parent task
        parent = ResearchTask(id=uuid4(), description="Parent", status=TaskStatus.PENDING)
        await repo.upsert_task_snapshot(parent)

        # Create child tasks
        child1 = ResearchTask(id=uuid4(), description="Child 1", parent_task_id=parent.id)
        child2 = ResearchTask(id=uuid4(), description="Child 2", parent_task_id=parent.id)

        await repo.upsert_task_snapshot(child1)
        await repo.upsert_task_snapshot(child2)
        await async_session.commit()

        # Get children
        children = await repo.get_children_ids(parent.id)

        assert len(children) == 2
        assert child1.id in children
        assert child2.id in children

    @pytest.mark.asyncio
    async def test_get_root_tasks(self, async_session):
        """Test getting root tasks (tasks without parents)."""
        repo = TaskRepository(async_session)

        # Create root tasks
        root1 = ResearchTask(id=uuid4(), description="Root 1")
        root2 = ResearchTask(id=uuid4(), description="Root 2")

        # Create child task
        child = ResearchTask(id=uuid4(), description="Child", parent_task_id=root1.id)

        await repo.upsert_task_snapshot(root1)
        await repo.upsert_task_snapshot(root2)
        await repo.upsert_task_snapshot(child)
        await async_session.commit()

        # Get root tasks
        roots = await repo.get_root_tasks(skip=0, take=100)

        assert len(roots) == 2
        root_ids = [task.Id for task in roots]
        assert root1.id in root_ids
        assert root2.id in root_ids
        assert child.id not in root_ids

    @pytest.mark.asyncio
    async def test_query_tasks_by_status(self, async_session):
        """Test querying tasks by status."""
        repo = TaskRepository(async_session)

        # Create tasks with different statuses
        pending = ResearchTask(id=uuid4(), description="Pending", status=TaskStatus.PENDING)
        completed = ResearchTask(id=uuid4(), description="Completed", status=TaskStatus.COMPLETED)

        await repo.upsert_task_snapshot(pending)
        await repo.upsert_task_snapshot(completed)
        await async_session.commit()

        # Query by status
        results = await repo.query_tasks(status="Pending")

        assert len(results) == 1
        assert results[0].Status == "Pending"

    @pytest.mark.asyncio
    async def test_query_tasks_with_search_term(self, async_session):
        """Test querying tasks with search term."""
        repo = TaskRepository(async_session)

        # Create tasks
        quantum = ResearchTask(id=uuid4(), description="Explain quantum computing")
        classical = ResearchTask(id=uuid4(), description="Explain classical computing")

        await repo.upsert_task_snapshot(quantum)
        await repo.upsert_task_snapshot(classical)
        await async_session.commit()

        # Search for "quantum"
        results = await repo.query_tasks(search_term="quantum")

        assert len(results) == 1
        assert "quantum" in results[0].Description.lower()


class TestEventRepository:
    """Tests for EventRepository."""

    @pytest.mark.asyncio
    async def test_add_event(self, async_session):
        """Test adding a task event."""
        repo = EventRepository(async_session)

        task_id = uuid4()
        event = TaskEventEntity(
            Id=uuid4(),
            TaskId=task_id,
            EventType="StatusChanged",
            Status="Completed",
            Message="Task completed successfully",
            TimestampUtc=datetime.utcnow(),
            AgentRole="Executor",
        )

        await repo.add_event(event)
        await async_session.commit()

        # Verify event was added
        events = await repo.get_events([task_id])
        assert len(events) == 1
        assert events[0].EventType == "StatusChanged"

    @pytest.mark.asyncio
    async def test_get_events_multiple_tasks(self, async_session):
        """Test getting events for multiple tasks."""
        repo = EventRepository(async_session)

        task1_id = uuid4()
        task2_id = uuid4()

        # Add events for task 1
        event1 = TaskEventEntity(
            Id=uuid4(),
            TaskId=task1_id,
            EventType="Started",
            Status="Executing",
            TimestampUtc=datetime.utcnow(),
        )

        # Add events for task 2
        event2 = TaskEventEntity(
            Id=uuid4(),
            TaskId=task2_id,
            EventType="Completed",
            Status="Completed",
            TimestampUtc=datetime.utcnow(),
        )

        await repo.add_event(event1)
        await repo.add_event(event2)
        await async_session.commit()

        # Get events for both tasks
        events = await repo.get_events([task1_id, task2_id])

        assert len(events) == 2
        task_ids = {event.TaskId for event in events}
        assert task1_id in task_ids
        assert task2_id in task_ids

    @pytest.mark.asyncio
    async def test_get_events_with_pagination(self, async_session):
        """Test event pagination."""
        repo = EventRepository(async_session)

        task_id = uuid4()

        # Add multiple events
        for i in range(10):
            event = TaskEventEntity(
                Id=uuid4(),
                TaskId=task_id,
                EventType=f"Event{i}",
                Status="Pending",
                TimestampUtc=datetime.utcnow(),
            )
            await repo.add_event(event)

        await async_session.commit()

        # Get first 5 events
        events = await repo.get_events([task_id], skip=0, take=5)
        assert len(events) == 5

        # Get next 5 events
        events = await repo.get_events([task_id], skip=5, take=5)
        assert len(events) == 5

    @pytest.mark.asyncio
    async def test_query_events_by_task_id(self, async_session):
        """Test querying events by task ID."""
        repo = EventRepository(async_session)

        task_id = uuid4()
        event = TaskEventEntity(
            Id=uuid4(),
            TaskId=task_id,
            EventType="Test",
            Status="Pending",
            TimestampUtc=datetime.utcnow(),
        )

        await repo.add_event(event)
        await async_session.commit()

        # Query by task ID
        events = await repo.query_events(task_id=task_id)

        assert len(events) == 1
        assert events[0].TaskId == task_id


class TestReportRepository:
    """Tests for ReportRepository."""

    @pytest.mark.asyncio
    async def test_upsert_report_create(self, async_session):
        """Test creating a new report."""
        repo = ReportRepository(async_session)

        task_id = uuid4()
        markdown = "# Test Report\n\nThis is a test report."
        generated_at = datetime.utcnow()

        await repo.upsert_report(task_id, markdown, generated_at)
        await async_session.commit()

        # Verify report was created
        result = await repo.get(task_id)
        assert result is not None
        assert result.ReportMarkdown == markdown
        assert result.TaskId == task_id

    @pytest.mark.asyncio
    async def test_upsert_report_update(self, async_session):
        """Test updating an existing report."""
        repo = ReportRepository(async_session)

        task_id = uuid4()
        original_markdown = "# Original Report"
        updated_markdown = "# Updated Report"

        # Create report
        await repo.upsert_report(task_id, original_markdown, datetime.utcnow())
        await async_session.commit()

        # Update report
        await repo.upsert_report(task_id, updated_markdown, datetime.utcnow())
        await async_session.commit()

        # Verify update
        result = await repo.get(task_id)
        assert result is not None
        assert result.ReportMarkdown == updated_markdown

    @pytest.mark.asyncio
    async def test_get_nonexistent_report(self, async_session):
        """Test getting a report that doesn't exist."""
        repo = ReportRepository(async_session)

        result = await repo.get(uuid4())
        assert result is None
