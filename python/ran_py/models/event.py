"""Task event models matching .NET entities."""
from datetime import datetime
from uuid import UUID

from pydantic import BaseModel, Field

from ran_py.models.task import TaskStatus


class TaskEvent(BaseModel):
    """Task event matching .NET TaskEvent."""

    task_id: UUID
    status: TaskStatus
    event_type: str = ""  # submitted, status, decomposed, aggregated, completed, failed
    timestamp_utc: datetime = Field(default_factory=lambda: datetime.utcnow())
    message: str | None = None
    parent_task_id: UUID | None = None

    class Config:
        """Pydantic configuration."""

        use_enum_values = True
