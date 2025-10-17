"""Task Analyzer Agent matching .NET TaskAnalyzerAgent.

Analyzes task complexity and decomposes complex tasks into subtasks.
"""
import re
from typing import Any

from ran_py.llm.provider import get_llm_provider
from ran_py.llm.structured_output import get_structured_output_retry
from ran_py.logging_config import get_logger
from ran_py.models import ComplexityAnalysis, ResearchTask
from ran_py.config import settings

logger = get_logger(__name__)


class TaskAnalyzerAgent:
    """Task analyzer agent for complexity analysis and task decomposition.

    Matches .NET TaskAnalyzerAgent functionality.
    """

    def __init__(self) -> None:
        """Initialize task analyzer agent."""
        self.role = "TaskAnalyzer"
        self.llm = get_llm_provider().get_chat_model()
        self.log_prompts = settings.research_agent.log_prompts

    async def analyze_complexity(self, task: ResearchTask) -> ComplexityAnalysis:
        """Analyze task complexity.

        Args:
            task: Research task to analyze

        Returns:
            Complexity analysis result
        """
        prompt = f"""Analyze this research task complexity and whether it requires decomposition.
Task: {task.description}

Consider:
1. Number of distinct sub-topics
2. Depth of analysis required
3. Domain expertise needed"""

        try:
            analysis = await get_structured_output_retry(
                self.llm,
                prompt,
                ComplexityAnalysis,
                max_retries=3,
                log_prompts=self.log_prompts,
            )
            return analysis
        except Exception as e:
            logger.error(f"Failed to analyze complexity: {e}")
            # Fallback to simple analysis
            return ComplexityAnalysis(
                requires_decomposition=False,
                complexity=0,
                reasoning="Failed to analyze complexity",
            )

    def is_multi_language_pattern(self, description: str) -> bool:
        """Check if description indicates multi-language scope.

        Args:
            description: Task description

        Returns:
            True if multi-language pattern detected
        """
        if not description:
            return False

        # Pattern: (Language1) ... and ... (Language2)
        pattern = r"\([^)]+\)\s*.*\s+and\s+.*\([^)]+\)"
        if re.search(pattern, description, re.IGNORECASE):
            return True

        # Check for multiple language names
        languages = ["English", "Español", "Spanish", "Français", "French", "Deutsch", "German"]
        hits = sum(1 for lang in languages if lang.lower() in description.lower())
        return hits >= 2

    def is_technical_multi_step_pattern(self, description: str) -> bool:
        """Check if description indicates technical multi-step pipeline.

        Args:
            description: Task description

        Returns:
            True if multi-step pattern detected
        """
        if not description:
            return False

        # Simple signal: many commas implies list of steps
        if description.count(",") >= 3:
            return True

        # Check for technical keywords
        keywords = [
            "data preprocessing",
            "preprocessing",
            "feature",
            "model selection",
            "training",
            "fine-tuning",
            "evaluation",
            "validation",
            "testing",
            "deployment",
            "monitoring",
            "pipeline",
        ]
        hits = sum(1 for keyword in keywords if keyword.lower() in description.lower())
        return hits >= 3

    async def decompose_task(self, task: ResearchTask) -> list[ResearchTask]:
        """Decompose task into subtasks.

        Args:
            task: Research task to decompose

        Returns:
            List of subtasks
        """
        prompt = f"""Decompose this research task into 3-5 focused subtasks that can be researched independently.
Task: {task.description}

Return only an array of subtask descriptions."""

        descriptions: list[str] = []

        try:
            # Try to get list of strings directly
            from pydantic import BaseModel, Field

            class SubtaskList(BaseModel):
                """List of subtask descriptions."""

                subtasks: list[str] = Field(description="List of subtask descriptions")

            result = await get_structured_output_retry(
                self.llm,
                prompt,
                SubtaskList,
                max_retries=3,
                log_prompts=self.log_prompts,
            )
            descriptions = result.subtasks

        except Exception as e:
            logger.warning(f"Failed to decompose task with structured output: {e}")
            # Fallback: just use the original task
            descriptions = [task.description]

        # Ensure stability for very long/complex requests (min 5)
        if len(task.description) > 300 and len(descriptions) < 5:
            extras = [
                "Background and definitions",
                "Current state of the art",
                "Challenges and limitations",
                "Applications and use cases",
                "Future outlook and trends",
            ]
            for extra in extras:
                if len(descriptions) >= 5:
                    break
                if not any(extra.lower() in d.lower() for d in descriptions):
                    desc_preview = task.description[:80] + "..." if len(task.description) > 80 else task.description
                    descriptions.append(f"{extra} for: {desc_preview}")

        # Ensure stability for technical multi-step tasks (min 4)
        if self.is_technical_multi_step_pattern(task.description) and len(descriptions) < 4:
            tech = [
                "Data preprocessing",
                "Model selection",
                "Training and hyperparameter tuning",
                "Evaluation and validation",
                "Deployment",
            ]
            for t in tech:
                if len(descriptions) >= 4:
                    break
                if not any(t.lower() in d.lower() for d in descriptions):
                    desc_preview = task.description[:80] + "..." if len(task.description) > 80 else task.description
                    descriptions.append(f"{t} for: {desc_preview}")

        # Store decomposition in metadata
        task.metadata["Decomposition"] = descriptions

        # Create subtasks
        return [ResearchTask(description=d, parent_task_id=task.id, priority=task.priority) for d in descriptions]

    async def process(self, task: ResearchTask) -> dict[str, Any]:
        """Process task for complexity analysis and potential decomposition.

        Matches .NET TaskAnalyzerAgent.ProcessAsync() method.

        Args:
            task: Research task to process

        Returns:
            Agent response with subtasks or execution flag
        """
        # Analyze complexity
        complexity = await self.analyze_complexity(task)

        # Apply heuristics
        if self.is_multi_language_pattern(task.description):
            complexity.requires_decomposition = True
            if complexity.reasoning:
                complexity.reasoning += "; multi-language scope detected"
            else:
                complexity.reasoning = "Detected multi-language scope; requires decomposition"

        if self.is_technical_multi_step_pattern(task.description):
            complexity.requires_decomposition = True
            if complexity.reasoning:
                complexity.reasoning += "; multi-step pipeline detected"
            else:
                complexity.reasoning = "Detected multi-step technical pipeline; requires decomposition"

        # Store analysis in metadata
        task.metadata["ComplexityAnalysis"] = complexity.model_dump()

        # Decompose if needed
        if complexity.requires_decomposition:
            subtasks = await self.decompose_task(task)
            logger.info(f"Task {task.id} decomposed into {len(subtasks)} subtasks")
            return {"success": True, "data": {"subtasks": subtasks}}

        logger.info(f"Task {task.id} ready for direct execution")
        return {"success": True, "data": {"ready_for_execution": True}}
