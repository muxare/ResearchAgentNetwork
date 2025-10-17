"""Executor Agent matching .NET ExecutorAgent.

Executes atomic research tasks and produces structured results.
"""
import re
from typing import Any

from pydantic import BaseModel, Field

from ran_py.config import settings
from ran_py.llm.provider import get_llm_provider
from ran_py.llm.structured_output import get_structured_output_retry
from ran_py.logging_config import get_logger
from ran_py.models import ResearchResult, ResearchTask

logger = get_logger(__name__)


class ExecutorStructuredResult(BaseModel):
    """Structured result from executor LLM call."""

    content: str = Field(description="Research content")
    sources: list[str] = Field(default_factory=list, description="List of sources")


class AtomicityCheck(BaseModel):
    """Result of atomicity check."""

    is_atomic: bool = Field(description="Whether task is atomic enough for execution")
    reasoning: str = Field(default="", description="Reasoning for atomicity determination")


class QualityScore(BaseModel):
    """Quality score for research content."""

    score: float = Field(description="Quality score from 0.0 to 1.0")


class Completeness(BaseModel):
    """Completeness assessment."""

    needs_more_research: bool = Field(description="Whether more research is needed")
    reasoning: str = Field(default="", description="Reasoning for completeness assessment")


class ExecutorAgent:
    """Executor agent for atomic task execution.

    Matches .NET ExecutorAgent functionality.
    """

    def __init__(self) -> None:
        """Initialize executor agent."""
        self.role = "Executor"
        self.llm = get_llm_provider().get_chat_model()
        self.log_prompts = settings.research_agent.log_prompts

    def build_context(self, task: ResearchTask) -> str:
        """Build execution context from task metadata.

        Args:
            task: Research task

        Returns:
            Context string
        """
        base_context = f"Research context for: {task.description}"

        # Check for retrieved context in metadata
        if "RetrievedContext" in task.metadata:
            ctx_obj = task.metadata["RetrievedContext"]
            sections = []

            # Support both legacy list of strings and new list of retrieved items
            if isinstance(ctx_obj, list):
                for item in ctx_obj[:3]:  # Take top 3 items
                    if isinstance(item, dict):
                        # RetrievedItem format
                        snippet = item.get("snippet", "")
                        if not snippet:
                            continue

                        kind = item.get("kind", "")
                        title = item.get("title", "")
                        url = item.get("url", "")
                        score = item.get("score")

                        if kind == "web":
                            header = f"[WEB] {title} ({url})"
                        else:
                            header = "[MEMORY]"

                        meta = f" score={score:.3f}" if score is not None else ""
                        sections.append(f"{header}{meta}\n{snippet}")
                    elif isinstance(item, str) and item.strip():
                        # Legacy string format
                        sections.append(item)

            if sections:
                joined = "\n---\n".join(sections)
                if len(joined) > 2000:
                    joined = joined[:2000]
                return base_context + "\n\nRetrieved Context:\n" + joined

        return base_context

    def sanitize_content(self, content: str) -> str:
        """Sanitize LLM-generated content.

        Args:
            content: Raw content

        Returns:
            Sanitized content
        """
        if not content or not content.strip():
            return ""

        text = content.strip()

        # Remove code fences
        if text.startswith("```"):
            idx = text.find("\n")
            if idx > 0:
                text = text[idx + 1 :]
            end = text.rfind("```")
            if end > 0:
                text = text[:end]

        # Remove basic HTML tags
        text = re.sub(r"<[^>]+>", "", text)

        # Normalize newlines
        text = text.replace("\r\n", "\n").replace("\r", "\n")

        return text

    async def is_atomic_task(self, task: ResearchTask) -> bool:
        """Check if task is atomic enough for direct execution.

        Args:
            task: Research task

        Returns:
            True if task is atomic
        """
        prompt = f"""Is this research task atomic enough for direct execution?
Task: {task.description}

Consider if it's focused, specific, and can be answered directly."""

        try:
            atomicity = await get_structured_output_retry(
                self.llm,
                prompt,
                AtomicityCheck,
                max_retries=3,
                log_prompts=self.log_prompts,
            )
            return atomicity.is_atomic
        except Exception as e:
            logger.error(f"Failed to check atomicity: {e}")
            # Fallback: assume atomic if check fails
            return True

    async def evaluate_quality(self, content: str) -> float:
        """Evaluate quality of research content.

        Args:
            content: Research content

        Returns:
            Quality score from 0.0 to 1.0
        """
        prompt = f"""Rate the quality of this research content from 0.0 to 1.0.
Content:
{content}

Consider comprehensiveness, accuracy, and relevance."""

        try:
            quality = await get_structured_output_retry(
                self.llm,
                prompt,
                QualityScore,
                max_retries=3,
                log_prompts=self.log_prompts,
            )
            return quality.score
        except Exception as e:
            logger.error(f"Failed to evaluate quality: {e}")
            # Fallback: neutral score
            return 0.5

    async def check_completeness(self, content: str, task: ResearchTask) -> bool:
        """Check if research content is complete or needs more investigation.

        Args:
            content: Research content
            task: Original research task

        Returns:
            True if more research is needed
        """
        prompt = f"""Assess whether this research content requires additional investigation.
Task: {task.description}
Content:
{content}"""

        try:
            completeness = await get_structured_output_retry(
                self.llm,
                prompt,
                Completeness,
                max_retries=3,
                log_prompts=self.log_prompts,
            )
            return completeness.needs_more_research
        except Exception as e:
            logger.error(f"Failed to check completeness: {e}")
            # Fallback: assume complete
            return False

    async def execute_with_llm(self, task: ResearchTask) -> ResearchResult:
        """Execute research task with LLM.

        Args:
            task: Research task to execute

        Returns:
            Research result with content and sources
        """
        context = self.build_context(task)
        prompt = f"""Research the following topic thoroughly and produce a structured result.
Topic: {task.description}

Context: {context}

Return ONLY valid JSON with fields content (string) and sources (array of strings). No prose, no markdown, no HTML. Escape newlines in content as \\n."""

        try:
            structured = await get_structured_output_retry(
                self.llm,
                prompt,
                ExecutorStructuredResult,
                max_retries=3,
                log_prompts=self.log_prompts,
            )

            content = structured.content or ""
            sources = [s for s in structured.sources if s.strip()]

            # Sanitize and evaluate
            sanitized = self.sanitize_content(content)
            quality = await self.evaluate_quality(sanitized)
            completeness = await self.check_completeness(sanitized, task)

            # Store metadata
            task.metadata["QualityScore"] = quality
            task.metadata["Completeness"] = {
                "NeedsMoreResearch": completeness,
                "Reasoning": "",
            }

            return ResearchResult(
                content=content,
                sources=sources,
                confidence_score=quality,
                requires_additional_research=completeness,
            )

        except Exception as e:
            logger.error(f"Failed to execute task with LLM: {e}")
            # Return empty result on failure
            return ResearchResult(
                content=f"Failed to execute research: {str(e)}",
                sources=[],
                confidence_score=0.0,
                requires_additional_research=True,
            )

    async def process(self, task: ResearchTask) -> dict[str, Any]:
        """Process task for execution.

        Matches .NET ExecutorAgent.ProcessAsync() method.

        Args:
            task: Research task to process

        Returns:
            Agent response with research result
        """
        # Check for forced execution
        if task.metadata.get("ForceExecute") is True:
            result = await self.execute_with_llm(task)
            logger.info(f"Task {task.id} executed (forced)")
            return {"success": True, "data": result}

        # Check if task is atomic
        if await self.is_atomic_task(task):
            result = await self.execute_with_llm(task)
            logger.info(f"Task {task.id} executed (atomic)")
            return {"success": True, "data": result}

        logger.warning(f"Task {task.id} not atomic enough for execution")
        return {"success": False, "message": "Task not atomic enough for execution"}
