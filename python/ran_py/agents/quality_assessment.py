"""Quality Assessment Agent matching .NET QualityAssessmentAgent.

Assesses research result quality and generates follow-up tasks if needed.
"""
from typing import Any

from pydantic import BaseModel, Field

from ran_py.config import settings
from ran_py.llm.provider import get_llm_provider
from ran_py.llm.structured_output import get_structured_output_retry
from ran_py.logging_config import get_logger
from ran_py.models import QualityAssessment, ResearchTask

logger = get_logger(__name__)


class FollowUpTaskList(BaseModel):
    """List of follow-up task descriptions."""

    tasks: list[str] = Field(description="List of follow-up task descriptions")


class QualityAssessmentAgent:
    """Quality assessment agent for result evaluation.

    Matches .NET QualityAssessmentAgent functionality.
    """

    def __init__(self) -> None:
        """Initialize quality assessment agent."""
        self.role = "QualityAssessor"
        self.llm = get_llm_provider().get_chat_model()
        self.log_prompts = settings.research_agent.log_prompts

    async def assess_result_quality(self, task: ResearchTask) -> QualityAssessment:
        """Assess quality of research result.

        Args:
            task: Research task with result to assess

        Returns:
            Quality assessment
        """
        if not task.result:
            # No result to assess
            return QualityAssessment(
                needs_more_research=True,
                reasoning="No research result available",
                gaps=["Complete research is missing"],
            )

        prompt = f"""Assess the quality of this research result.
Task: {task.description}
Result: {task.result.content}

Return ONLY valid JSON with fields: needsMoreResearch (bool), reasoning (string), gaps (string array). No prose, no markdown, no HTML."""

        try:
            assessment = await get_structured_output_retry(
                self.llm,
                prompt,
                QualityAssessment,
                max_retries=3,
                log_prompts=self.log_prompts,
            )
            return assessment
        except Exception as e:
            logger.error(f"Failed to assess quality: {e}")
            # Fallback: assume quality is acceptable
            return QualityAssessment(
                needs_more_research=False,
                reasoning=f"Quality assessment failed: {str(e)}",
                gaps=[],
            )

    async def generate_follow_up_tasks(self, assessment: QualityAssessment, task: ResearchTask) -> list[ResearchTask]:
        """Generate follow-up tasks to address quality gaps.

        Args:
            assessment: Quality assessment with identified gaps
            task: Original research task

        Returns:
            List of follow-up research tasks
        """
        gaps_str = ", ".join(assessment.gaps) if assessment.gaps else "quality improvements"
        prompt = f"""Generate follow-up research tasks to address these gaps.
Original task: {task.description}
Gaps identified: {gaps_str}

Return ONLY a valid JSON array of strings. No prose, no markdown, no HTML."""

        try:
            result = await get_structured_output_retry(
                self.llm,
                prompt,
                FollowUpTaskList,
                max_retries=3,
                log_prompts=self.log_prompts,
            )

            descriptions = result.tasks
        except Exception as e:
            logger.error(f"Failed to generate follow-up tasks: {e}")
            # Fallback: create generic follow-up task
            descriptions = [f"Address quality gaps in: {task.description}"]

        # Create follow-up tasks
        follow_up_tasks = [
            ResearchTask(
                description=desc,
                parent_task_id=task.id,
                priority=task.priority,
            )
            for desc in descriptions
            if desc.strip()
        ]

        return follow_up_tasks

    async def process(self, task: ResearchTask) -> dict[str, Any]:
        """Process task for quality assessment.

        Matches .NET QualityAssessmentAgent.ProcessAsync() method.

        Args:
            task: Research task with result to assess

        Returns:
            Agent response with follow-up tasks if needed
        """
        if not task.result:
            logger.warning(f"Task {task.id} has no result to assess")
            return {"success": False, "message": "No result to assess"}

        # Assess result quality
        assessment = await self.assess_result_quality(task)

        # Store assessment in metadata
        task.metadata["QualityAssessment"] = assessment.model_dump()

        # Generate follow-up tasks if needed
        if assessment.needs_more_research:
            follow_up_tasks = await self.generate_follow_up_tasks(assessment, task)
            logger.info(f"Task {task.id} needs {len(follow_up_tasks)} follow-up tasks")
            return {"success": True, "data": {"follow_up_tasks": follow_up_tasks}}

        logger.info(f"Task {task.id} quality assessment passed")
        return {"success": True, "data": {"assessment": assessment}}
