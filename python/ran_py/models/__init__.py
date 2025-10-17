"""Domain models matching .NET entities."""
from ran_py.models.event import TaskEvent
from ran_py.models.graph_state import FinalizationState, OrchestrationState
from ran_py.models.report import OutlineSection, ReportOutline, ReportSectionDraft
from ran_py.models.task import (
    ComplexityAnalysis,
    QualityAssessment,
    ResearchResult,
    ResearchTask,
    TaskCategory,
    TaskStatus,
)

__all__ = [
    # Task models
    "TaskStatus",
    "TaskCategory",
    "ResearchResult",
    "ResearchTask",
    "ComplexityAnalysis",
    "QualityAssessment",
    # Event models
    "TaskEvent",
    # Report models
    "OutlineSection",
    "ReportOutline",
    "ReportSectionDraft",
    # Graph state models
    "OrchestrationState",
    "FinalizationState",
]
