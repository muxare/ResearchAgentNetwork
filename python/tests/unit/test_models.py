"""Unit tests for Pydantic models."""
import pytest
from uuid import uuid4

from ran_py.models import (
    ComplexityAnalysis,
    QualityAssessment,
    ResearchResult,
    ResearchTask,
    TaskCategory,
    TaskStatus,
)


class TestTaskStatus:
    """Tests for TaskStatus enum."""

    def test_task_status_values(self):
        """Test TaskStatus enum values match .NET."""
        assert TaskStatus.PENDING == "Pending"
        assert TaskStatus.ANALYZING == "Analyzing"
        assert TaskStatus.EXECUTING == "Executing"
        assert TaskStatus.AGGREGATING == "Aggregating"
        assert TaskStatus.COMPLETED == "Completed"
        assert TaskStatus.FAILED == "Failed"


class TestTaskCategory:
    """Tests for TaskCategory enum."""

    def test_task_category_values(self):
        """Test TaskCategory enum values match .NET."""
        assert TaskCategory.NORMAL == "Normal"
        assert TaskCategory.FINALIZATION == "Finalization"


class TestResearchTask:
    """Tests for ResearchTask model."""

    def test_create_task_with_defaults(self):
        """Test creating a task with default values."""
        task = ResearchTask(description="Test task")

        assert task.description == "Test task"
        assert task.status == TaskStatus.PENDING
        assert task.priority == 5
        assert task.parent_task_id is None
        assert len(task.sub_task_ids) == 0
        assert task.result is None
        assert len(task.metadata) == 0
        assert task.is_system_task == False
        assert task.category == TaskCategory.NORMAL

    def test_create_task_with_custom_values(self):
        """Test creating a task with custom values."""
        task_id = uuid4()
        parent_id = uuid4()

        task = ResearchTask(
            id=task_id,
            description="Custom task",
            status=TaskStatus.EXECUTING,
            priority=8,
            parent_task_id=parent_id,
            is_system_task=True,
            category=TaskCategory.FINALIZATION,
        )

        assert task.id == task_id
        assert task.description == "Custom task"
        assert task.status == TaskStatus.EXECUTING
        assert task.priority == 8
        assert task.parent_task_id == parent_id
        assert task.is_system_task == True
        assert task.category == TaskCategory.FINALIZATION

    def test_task_with_result(self):
        """Test task with research result."""
        result = ResearchResult(
            content="Test content",
            confidence_score=0.9,
            sources=["Source 1", "Source 2"],
        )

        task = ResearchTask(description="Test", result=result)

        assert task.result is not None
        assert task.result.content == "Test content"
        assert task.result.confidence_score == 0.9
        assert len(task.result.sources) == 2

    def test_task_with_metadata(self):
        """Test task with metadata."""
        task = ResearchTask(
            description="Test",
            metadata={"key1": "value1", "key2": 123},
        )

        assert task.metadata["key1"] == "value1"
        assert task.metadata["key2"] == 123


class TestResearchResult:
    """Tests for ResearchResult model."""

    def test_create_result_with_defaults(self):
        """Test creating a result with default values."""
        result = ResearchResult()

        assert result.content == ""
        assert result.confidence_score == 0.0
        assert len(result.sources) == 0
        assert len(result.metadata) == 0
        assert result.requires_additional_research == False

    def test_create_result_with_custom_values(self):
        """Test creating a result with custom values."""
        result = ResearchResult(
            content="Research findings",
            confidence_score=0.85,
            sources=["Source A", "Source B"],
            requires_additional_research=True,
            metadata={"detail": "value"},
        )

        assert result.content == "Research findings"
        assert result.confidence_score == 0.85
        assert len(result.sources) == 2
        assert result.requires_additional_research == True
        assert result.metadata["detail"] == "value"


class TestComplexityAnalysis:
    """Tests for ComplexityAnalysis model."""

    def test_create_analysis(self):
        """Test creating a complexity analysis."""
        analysis = ComplexityAnalysis(
            requires_decomposition=True,
            complexity=7,
            reasoning="Task is too complex for direct execution",
        )

        assert analysis.requires_decomposition == True
        assert analysis.complexity == 7
        assert "too complex" in analysis.reasoning


class TestQualityAssessment:
    """Tests for QualityAssessment model."""

    def test_create_assessment_no_gaps(self):
        """Test creating a quality assessment with no gaps."""
        assessment = QualityAssessment(
            needs_more_research=False,
            reasoning="Content is comprehensive",
            gaps=[],
        )

        assert assessment.needs_more_research == False
        assert len(assessment.gaps) == 0

    def test_create_assessment_with_gaps(self):
        """Test creating a quality assessment with gaps."""
        assessment = QualityAssessment(
            needs_more_research=True,
            reasoning="Missing key information",
            gaps=["Gap 1", "Gap 2", "Gap 3"],
        )

        assert assessment.needs_more_research == True
        assert len(assessment.gaps) == 3
        assert "Gap 1" in assessment.gaps
