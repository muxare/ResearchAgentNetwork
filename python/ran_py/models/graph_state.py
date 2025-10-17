"""LangGraph state models.

These models define the state that flows through the LangGraph orchestrator.
Unlike LangChain memory, these are explicit typed state objects.
"""
from typing import Any, TypedDict

from ran_py.models.task import ComplexityAnalysis, QualityAssessment, ResearchResult, ResearchTask


class OrchestrationState(TypedDict, total=False):
    """State for the main orchestration graph.

    This state flows through the LangGraph nodes and represents the current
    state of task processing. Uses TypedDict for LangGraph compatibility.

    Attributes:
        task: The research task being processed
        complexity_analysis: Analysis from TaskAnalyzerAgent
        result: Execution result from ExecutorAgent
        quality_assessment: Quality evaluation from QualityAssessmentAgent
        follow_up_tasks: Additional tasks identified during processing
        error: Error message if processing failed
        metadata: Additional context for processing
    """

    task: ResearchTask
    complexity_analysis: ComplexityAnalysis | None
    result: ResearchResult | None
    quality_assessment: QualityAssessment | None
    follow_up_tasks: list[ResearchTask]
    error: str | None
    metadata: dict[str, Any]


class FinalizationState(TypedDict, total=False):
    """State for the finalization sub-graph.

    This state flows through the report generation pipeline.

    Attributes:
        task_id: Root task ID for the report
        outline: Generated report outline
        section_drafts: Drafted sections
        fact_checked_sections: Sections after fact-checking
        final_report: Final markdown report
        bibliography: Formatted bibliography
        error: Error message if finalization failed
    """

    task_id: str
    outline: dict[str, Any] | None
    section_drafts: list[dict[str, Any]]
    fact_checked_sections: list[dict[str, Any]]
    final_report: str | None
    bibliography: str | None
    error: str | None
