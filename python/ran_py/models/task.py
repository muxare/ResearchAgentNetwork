"""Research task models matching .NET entities."""
from datetime import datetime
from enum import Enum
from typing import Any
from uuid import UUID, uuid4

from pydantic import BaseModel, Field


class TaskStatus(str, Enum):
    """Task status enum matching .NET TaskStatus."""

    PENDING = "Pending"
    ANALYZING = "Analyzing"
    EXECUTING = "Executing"
    AGGREGATING = "Aggregating"
    COMPLETED = "Completed"
    FAILED = "Failed"


class TaskCategory(str, Enum):
    """Task category enum matching .NET TaskCategory."""

    NORMAL = "Normal"
    FINALIZATION = "Finalization"


class ResearchResult(BaseModel):
    """Research result matching .NET ResearchResult."""

    content: str = ""
    confidence_score: float = 0.0
    sources: list[str] = Field(default_factory=list)
    metadata: dict[str, Any] = Field(default_factory=dict)
    requires_additional_research: bool = False


class ResearchTask(BaseModel):
    """Research task matching .NET ResearchTask."""

    id: UUID = Field(default_factory=uuid4)
    description: str = ""
    status: TaskStatus = TaskStatus.PENDING
    priority: int = 5
    parent_task_id: UUID | None = None
    sub_task_ids: list[UUID] = Field(default_factory=list)
    result: ResearchResult | None = None
    metadata: dict[str, Any] = Field(default_factory=dict)
    created_at: datetime = Field(default_factory=lambda: datetime.utcnow())
    is_system_task: bool = False
    category: TaskCategory = TaskCategory.NORMAL

    class Config:
        """Pydantic configuration."""

        use_enum_values = True


class ComplexityAnalysis(BaseModel):
    """Complexity analysis matching .NET ComplexityAnalysis."""

    requires_decomposition: bool = False
    complexity: int = 0
    reasoning: str = ""


class QualityAssessment(BaseModel):
    """Quality assessment matching .NET QualityAssessment."""

    needs_more_research: bool = False
    reasoning: str = ""
    gaps: list[str] = Field(default_factory=list)
