"""Pytest configuration and shared fixtures."""
import os
import sys
from pathlib import Path
from unittest.mock import AsyncMock, Mock
from uuid import uuid4

import pytest

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))


@pytest.fixture
def mock_llm():
    """Mock LLM for testing agents without actual API calls."""
    llm = AsyncMock()
    llm.ainvoke = AsyncMock()
    return llm


@pytest.fixture
def sample_research_task():
    """Create a sample research task for testing."""
    from ran_py.models import ResearchTask, TaskStatus

    return ResearchTask(
        id=uuid4(),
        description="Explain the basics of quantum computing",
        status=TaskStatus.PENDING,
        priority=5,
    )


@pytest.fixture
def sample_research_task_with_result():
    """Create a sample research task with a result for testing."""
    from ran_py.models import ResearchTask, ResearchResult, TaskStatus

    task = ResearchTask(
        id=uuid4(),
        description="Explain the basics of quantum computing",
        status=TaskStatus.COMPLETED,
        priority=5,
    )
    task.result = ResearchResult(
        content="Quantum computing uses quantum bits (qubits) which can exist in superposition...",
        confidence_score=0.85,
        sources=["Source 1", "Source 2"],
    )
    return task


@pytest.fixture
def sample_complexity_analysis():
    """Create a sample complexity analysis."""
    from ran_py.models import ComplexityAnalysis

    return ComplexityAnalysis(
        requires_decomposition=False,
        complexity=3,
        reasoning="Task is focused and specific enough for direct execution",
    )


@pytest.fixture
def sample_quality_assessment():
    """Create a sample quality assessment."""
    from ran_py.models import QualityAssessment

    return QualityAssessment(
        needs_more_research=False,
        reasoning="Content is comprehensive and well-sourced",
        gaps=[],
    )


@pytest.fixture
def mock_structured_output():
    """Mock for structured output helper."""
    async def _mock_get_structured_output(llm, prompt, model_class, max_retries=3, log_prompts=False):
        """Return a mock instance of the model class."""
        # Create instance with default values
        return model_class()

    return _mock_get_structured_output


@pytest.fixture(autouse=True)
def reset_environment():
    """Reset environment variables for each test."""
    # Store original env vars
    original_env = os.environ.copy()

    # Set test environment
    os.environ["AI_PROVIDER"] = "Ollama"
    os.environ["OLLAMA_BASE_URL"] = "http://localhost:11434"
    os.environ["DATABASE_PROVIDER"] = "Sqlite"
    os.environ["DATABASE_PATH"] = ":memory:"
    os.environ["LOG_PROMPTS"] = "false"

    yield

    # Restore original env vars
    os.environ.clear()
    os.environ.update(original_env)
