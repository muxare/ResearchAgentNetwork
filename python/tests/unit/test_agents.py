"""Unit tests for agent implementations."""
import pytest
from unittest.mock import AsyncMock, Mock, patch
from uuid import uuid4

from ran_py.agents.executor import ExecutorAgent
from ran_py.agents.quality_assessment import QualityAssessmentAgent
from ran_py.agents.task_analyzer import TaskAnalyzerAgent
from ran_py.models import (
    ComplexityAnalysis,
    QualityAssessment,
    ResearchResult,
    ResearchTask,
    TaskStatus,
)


class TestTaskAnalyzerAgent:
    """Tests for TaskAnalyzerAgent."""

    @pytest.mark.asyncio
    async def test_analyze_complexity_success(self, sample_research_task):
        """Test successful complexity analysis."""
        agent = TaskAnalyzerAgent()

        # Mock the LLM call
        with patch("ran_py.agents.task_analyzer.get_structured_output_retry") as mock_llm:
            mock_llm.return_value = ComplexityAnalysis(
                requires_decomposition=False,
                complexity=3,
                reasoning="Task is straightforward",
            )

            result = await agent.analyze_complexity(sample_research_task)

            assert isinstance(result, ComplexityAnalysis)
            assert result.requires_decomposition == False
            assert result.complexity == 3
            assert len(result.reasoning) > 0

    @pytest.mark.asyncio
    async def test_analyze_complexity_fallback(self, sample_research_task):
        """Test complexity analysis fallback on error."""
        agent = TaskAnalyzerAgent()

        # Mock the LLM to raise an exception
        with patch("ran_py.agents.task_analyzer.get_structured_output_retry") as mock_llm:
            mock_llm.side_effect = Exception("LLM error")

            result = await agent.analyze_complexity(sample_research_task)

            assert isinstance(result, ComplexityAnalysis)
            assert result.requires_decomposition == False
            assert "Failed to analyze complexity" in result.reasoning

    def test_is_multi_language_pattern(self):
        """Test multi-language pattern detection."""
        agent = TaskAnalyzerAgent()

        # Test positive cases
        assert agent.is_multi_language_pattern("Compare (English) and (Spanish) grammar")
        assert agent.is_multi_language_pattern("Differences between English and French")

        # Test negative cases
        assert not agent.is_multi_language_pattern("Explain quantum physics")
        assert not agent.is_multi_language_pattern("Single language task")

    def test_is_technical_multi_step_pattern(self):
        """Test technical multi-step pattern detection."""
        agent = TaskAnalyzerAgent()

        # Test positive cases
        assert agent.is_technical_multi_step_pattern(
            "Build ML pipeline: preprocessing, feature engineering, model training, evaluation"
        )
        assert agent.is_technical_multi_step_pattern(
            "Data preprocessing, model selection, fine-tuning, validation, deployment"
        )

        # Test negative cases
        assert not agent.is_technical_multi_step_pattern("Simple explanation task")
        assert not agent.is_technical_multi_step_pattern("Short task")

    @pytest.mark.asyncio
    async def test_decompose_task(self, sample_research_task):
        """Test task decomposition."""
        agent = TaskAnalyzerAgent()

        with patch("ran_py.agents.task_analyzer.get_structured_output_retry") as mock_llm:
            # Mock subtask list
            from pydantic import BaseModel, Field

            class SubtaskList(BaseModel):
                subtasks: list[str] = Field(default_factory=list)

            mock_llm.return_value = SubtaskList(
                subtasks=[
                    "Explain quantum superposition",
                    "Explain quantum entanglement",
                    "Explain quantum gates",
                ]
            )

            subtasks = await agent.decompose_task(sample_research_task)

            assert len(subtasks) >= 3
            assert all(isinstance(task, ResearchTask) for task in subtasks)
            assert all(task.parent_task_id == sample_research_task.id for task in subtasks)

    @pytest.mark.asyncio
    async def test_process_no_decomposition(self, sample_research_task):
        """Test process when task doesn't need decomposition."""
        agent = TaskAnalyzerAgent()

        with patch("ran_py.agents.task_analyzer.get_structured_output_retry") as mock_llm:
            mock_llm.return_value = ComplexityAnalysis(
                requires_decomposition=False,
                complexity=2,
                reasoning="Simple task",
            )

            result = await agent.process(sample_research_task)

            assert result["success"] == True
            assert result["data"]["ready_for_execution"] == True

    @pytest.mark.asyncio
    async def test_process_with_decomposition(self, sample_research_task):
        """Test process when task needs decomposition."""
        agent = TaskAnalyzerAgent()

        with patch("ran_py.agents.task_analyzer.get_structured_output_retry") as mock_llm:
            # First call: complexity analysis
            # Second call: decomposition
            from pydantic import BaseModel, Field

            class SubtaskList(BaseModel):
                subtasks: list[str]

            mock_llm.side_effect = [
                ComplexityAnalysis(
                    requires_decomposition=True,
                    complexity=8,
                    reasoning="Complex task",
                ),
                SubtaskList(subtasks=["Subtask 1", "Subtask 2", "Subtask 3"]),
            ]

            result = await agent.process(sample_research_task)

            assert result["success"] == True
            assert "subtasks" in result["data"]
            assert len(result["data"]["subtasks"]) >= 3


class TestExecutorAgent:
    """Tests for ExecutorAgent."""

    @pytest.mark.asyncio
    async def test_is_atomic_task_true(self, sample_research_task):
        """Test atomicity check returns true for atomic task."""
        agent = ExecutorAgent()

        with patch("ran_py.agents.executor.get_structured_output_retry") as mock_llm:
            from ran_py.agents.executor import AtomicityCheck

            mock_llm.return_value = AtomicityCheck(
                is_atomic=True,
                reasoning="Task is focused and specific",
            )

            result = await agent.is_atomic_task(sample_research_task)

            assert result == True

    @pytest.mark.asyncio
    async def test_is_atomic_task_fallback(self, sample_research_task):
        """Test atomicity check fallback on error."""
        agent = ExecutorAgent()

        with patch("ran_py.agents.executor.get_structured_output_retry") as mock_llm:
            mock_llm.side_effect = Exception("LLM error")

            result = await agent.is_atomic_task(sample_research_task)

            # Fallback assumes atomic
            assert result == True

    @pytest.mark.asyncio
    async def test_evaluate_quality(self):
        """Test quality evaluation."""
        agent = ExecutorAgent()

        with patch("ran_py.agents.executor.get_structured_output_retry") as mock_llm:
            from ran_py.agents.executor import QualityScore

            mock_llm.return_value = QualityScore(score=0.85)

            result = await agent.evaluate_quality("High quality content")

            assert result == 0.85

    @pytest.mark.asyncio
    async def test_check_completeness_false(self, sample_research_task):
        """Test completeness check returns false when complete."""
        agent = ExecutorAgent()

        with patch("ran_py.agents.executor.get_structured_output_retry") as mock_llm:
            from ran_py.agents.executor import Completeness

            mock_llm.return_value = Completeness(
                needs_more_research=False,
                reasoning="Content is comprehensive",
            )

            result = await agent.check_completeness("Comprehensive content", sample_research_task)

            assert result == False

    def test_sanitize_content(self):
        """Test content sanitization."""
        agent = ExecutorAgent()

        # Test code fence removal (note: sanitize preserves newlines after trimming)
        content = "```python\nprint('hello')\n```"
        result = agent.sanitize_content(content)
        assert "print('hello')" in result

        # Test HTML tag removal
        content = "<p>Hello <b>world</b></p>"
        assert agent.sanitize_content(content) == "Hello world"

        # Test empty content
        assert agent.sanitize_content("") == ""
        assert agent.sanitize_content("   ") == ""

    def test_build_context(self):
        """Test context building from metadata."""
        agent = ExecutorAgent()

        task = ResearchTask(description="Test task")

        # Test with no retrieved context
        context = agent.build_context(task)
        assert "Research context for: Test task" in context

        # Test with retrieved context
        task.metadata["RetrievedContext"] = [
            {
                "kind": "web",
                "title": "Example",
                "url": "http://example.com",
                "snippet": "Example content",
                "score": 0.9,
            }
        ]
        context = agent.build_context(task)
        assert "Example content" in context
        assert "http://example.com" in context

    @pytest.mark.asyncio
    async def test_execute_with_llm_success(self, sample_research_task):
        """Test successful task execution."""
        agent = ExecutorAgent()

        with patch("ran_py.agents.executor.get_structured_output_retry") as mock_llm:
            from ran_py.agents.executor import ExecutorStructuredResult, QualityScore, Completeness

            # Mock multiple LLM calls
            mock_llm.side_effect = [
                ExecutorStructuredResult(
                    content="Quantum computing uses qubits...",
                    sources=["Source 1", "Source 2"],
                ),
                QualityScore(score=0.8),
                Completeness(needs_more_research=False, reasoning="Complete"),
            ]

            result = await agent.execute_with_llm(sample_research_task)

            assert isinstance(result, ResearchResult)
            assert len(result.content) > 0
            assert len(result.sources) == 2
            assert result.confidence_score == 0.8
            assert result.requires_additional_research == False

    @pytest.mark.asyncio
    async def test_execute_with_llm_failure(self, sample_research_task):
        """Test execution failure handling."""
        agent = ExecutorAgent()

        with patch("ran_py.agents.executor.get_structured_output_retry") as mock_llm:
            mock_llm.side_effect = Exception("LLM error")

            result = await agent.execute_with_llm(sample_research_task)

            assert isinstance(result, ResearchResult)
            assert "Failed to execute research" in result.content
            assert result.confidence_score == 0.0
            assert result.requires_additional_research == True

    @pytest.mark.asyncio
    async def test_process_forced_execution(self, sample_research_task):
        """Test process with forced execution."""
        sample_research_task.metadata["ForceExecute"] = True
        agent = ExecutorAgent()

        with patch.object(agent, "execute_with_llm") as mock_execute:
            mock_execute.return_value = ResearchResult(
                content="Result",
                sources=[],
                confidence_score=0.7,
            )

            result = await agent.process(sample_research_task)

            assert result["success"] == True
            assert "data" in result
            mock_execute.assert_called_once()

    @pytest.mark.asyncio
    async def test_process_non_atomic(self, sample_research_task):
        """Test process with non-atomic task."""
        agent = ExecutorAgent()

        with patch.object(agent, "is_atomic_task") as mock_atomic:
            mock_atomic.return_value = False

            result = await agent.process(sample_research_task)

            assert result["success"] == False
            assert "not atomic enough" in result["message"]


class TestQualityAssessmentAgent:
    """Tests for QualityAssessmentAgent."""

    @pytest.mark.asyncio
    async def test_assess_result_quality_no_result(self, sample_research_task):
        """Test assessment when task has no result."""
        agent = QualityAssessmentAgent()

        result = await agent.assess_result_quality(sample_research_task)

        assert isinstance(result, QualityAssessment)
        assert result.needs_more_research == True
        assert "No research result available" in result.reasoning

    @pytest.mark.asyncio
    async def test_assess_result_quality_success(self, sample_research_task_with_result):
        """Test successful quality assessment."""
        agent = QualityAssessmentAgent()

        with patch("ran_py.agents.quality_assessment.get_structured_output_retry") as mock_llm:
            mock_llm.return_value = QualityAssessment(
                needs_more_research=False,
                reasoning="High quality content",
                gaps=[],
            )

            result = await agent.assess_result_quality(sample_research_task_with_result)

            assert isinstance(result, QualityAssessment)
            assert result.needs_more_research == False
            assert len(result.gaps) == 0

    @pytest.mark.asyncio
    async def test_assess_result_quality_fallback(self, sample_research_task_with_result):
        """Test quality assessment fallback on error."""
        agent = QualityAssessmentAgent()

        with patch("ran_py.agents.quality_assessment.get_structured_output_retry") as mock_llm:
            mock_llm.side_effect = Exception("LLM error")

            result = await agent.assess_result_quality(sample_research_task_with_result)

            assert isinstance(result, QualityAssessment)
            assert result.needs_more_research == False
            assert "Quality assessment failed" in result.reasoning

    @pytest.mark.asyncio
    async def test_generate_follow_up_tasks(self, sample_research_task):
        """Test follow-up task generation."""
        agent = QualityAssessmentAgent()
        assessment = QualityAssessment(
            needs_more_research=True,
            reasoning="Missing details",
            gaps=["Gap 1", "Gap 2"],
        )

        with patch("ran_py.agents.quality_assessment.get_structured_output_retry") as mock_llm:
            from ran_py.agents.quality_assessment import FollowUpTaskList

            mock_llm.return_value = FollowUpTaskList(
                tasks=["Follow-up 1", "Follow-up 2"]
            )

            result = await agent.generate_follow_up_tasks(assessment, sample_research_task)

            assert len(result) == 2
            assert all(isinstance(task, ResearchTask) for task in result)
            assert all(task.parent_task_id == sample_research_task.id for task in result)

    @pytest.mark.asyncio
    async def test_generate_follow_up_tasks_fallback(self, sample_research_task):
        """Test follow-up task generation fallback."""
        agent = QualityAssessmentAgent()
        assessment = QualityAssessment(
            needs_more_research=True,
            reasoning="Missing details",
            gaps=["Gap 1"],
        )

        with patch("ran_py.agents.quality_assessment.get_structured_output_retry") as mock_llm:
            mock_llm.side_effect = Exception("LLM error")

            result = await agent.generate_follow_up_tasks(assessment, sample_research_task)

            assert len(result) == 1
            assert "Address quality gaps" in result[0].description

    @pytest.mark.asyncio
    async def test_process_no_result(self, sample_research_task):
        """Test process when task has no result."""
        agent = QualityAssessmentAgent()

        result = await agent.process(sample_research_task)

        assert result["success"] == False
        assert "No result to assess" in result["message"]

    @pytest.mark.asyncio
    async def test_process_needs_follow_up(self, sample_research_task_with_result):
        """Test process when follow-up tasks are needed."""
        agent = QualityAssessmentAgent()

        with patch.object(agent, "assess_result_quality") as mock_assess:
            with patch.object(agent, "generate_follow_up_tasks") as mock_follow_up:
                mock_assess.return_value = QualityAssessment(
                    needs_more_research=True,
                    reasoning="Needs more",
                    gaps=["Gap 1"],
                )
                mock_follow_up.return_value = [
                    ResearchTask(description="Follow-up task")
                ]

                result = await agent.process(sample_research_task_with_result)

                assert result["success"] == True
                assert "follow_up_tasks" in result["data"]
                assert len(result["data"]["follow_up_tasks"]) == 1

    @pytest.mark.asyncio
    async def test_process_quality_passed(self, sample_research_task_with_result):
        """Test process when quality assessment passes."""
        agent = QualityAssessmentAgent()

        with patch.object(agent, "assess_result_quality") as mock_assess:
            mock_assess.return_value = QualityAssessment(
                needs_more_research=False,
                reasoning="Good quality",
                gaps=[],
            )

            result = await agent.process(sample_research_task_with_result)

            assert result["success"] == True
            assert "assessment" in result["data"]
            assert result["data"]["assessment"].needs_more_research == False
