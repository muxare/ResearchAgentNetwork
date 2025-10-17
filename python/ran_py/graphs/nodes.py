"""LangGraph node functions.

These functions wrap agent logic into LangGraph-compatible nodes.
Each node receives state and returns state updates.
"""
from typing import Any

from ran_py.agents.executor import ExecutorAgent
from ran_py.agents.quality_assessment import QualityAssessmentAgent
from ran_py.agents.task_analyzer import TaskAnalyzerAgent
from ran_py.logging_config import get_logger
from ran_py.models import OrchestrationState, ResearchTask, TaskStatus

logger = get_logger(__name__)


async def analyze_node(state: OrchestrationState) -> dict[str, Any]:
    """Analyze task complexity and determine if decomposition is needed.

    This is a LangGraph node function that wraps TaskAnalyzerAgent.

    Args:
        state: Current orchestration state

    Returns:
        State updates with complexity analysis
    """
    task = state["task"]
    logger.info(f"[analyze_node] Processing task {task.id}")

    # Update task status
    task.status = TaskStatus.ANALYZING

    # Run analyzer agent
    agent = TaskAnalyzerAgent()
    response = await agent.process(task)

    if not response.get("success"):
        error_msg = response.get("message", "Analysis failed")
        logger.error(f"[analyze_node] Failed: {error_msg}")
        return {"task": task, "error": error_msg}

    data = response.get("data", {})

    # Check if task needs decomposition
    if "subtasks" in data:
        subtasks = data["subtasks"]
        logger.info(f"[analyze_node] Task requires decomposition into {len(subtasks)} subtasks")
        # Note: Actual subtask handling would be done by orchestrator
        # For now, just store in metadata
        task.metadata["RequiresDecomposition"] = True
        task.metadata["SubtaskCount"] = len(subtasks)
        return {
            "task": task,
            "complexity_analysis": task.metadata.get("ComplexityAnalysis"),
            "metadata": {"requires_decomposition": True, "subtasks": subtasks},
        }

    # Task is ready for execution
    logger.info(f"[analyze_node] Task ready for execution")
    return {
        "task": task,
        "complexity_analysis": task.metadata.get("ComplexityAnalysis"),
        "metadata": {"requires_decomposition": False},
    }


async def execute_node(state: OrchestrationState) -> dict[str, Any]:
    """Execute atomic research task.

    This is a LangGraph node function that wraps ExecutorAgent.

    Args:
        state: Current orchestration state

    Returns:
        State updates with research result
    """
    task = state["task"]
    logger.info(f"[execute_node] Executing task {task.id}")

    # Update task status
    task.status = TaskStatus.EXECUTING

    # Run executor agent
    agent = ExecutorAgent()
    response = await agent.process(task)

    if not response.get("success"):
        error_msg = response.get("message", "Execution failed")
        logger.error(f"[execute_node] Failed: {error_msg}")
        task.status = TaskStatus.FAILED
        return {"task": task, "error": error_msg}

    # Extract result
    result = response.get("data")
    task.result = result
    logger.info(f"[execute_node] Task executed successfully, confidence: {result.confidence_score:.2f}")

    return {"task": task, "result": result}


async def assess_quality_node(state: OrchestrationState) -> dict[str, Any]:
    """Assess research result quality.

    This is a LangGraph node function that wraps QualityAssessmentAgent.

    Args:
        state: Current orchestration state

    Returns:
        State updates with quality assessment
    """
    task = state["task"]
    logger.info(f"[assess_quality_node] Assessing task {task.id}")

    # Run quality assessment agent
    agent = QualityAssessmentAgent()
    response = await agent.process(task)

    if not response.get("success"):
        error_msg = response.get("message", "Quality assessment failed")
        logger.error(f"[assess_quality_node] Failed: {error_msg}")
        return {"task": task, "error": error_msg}

    data = response.get("data", {})

    # Check if follow-up tasks are needed
    if "follow_up_tasks" in data:
        follow_up_tasks = data["follow_up_tasks"]
        logger.info(f"[assess_quality_node] Generated {len(follow_up_tasks)} follow-up tasks")
        return {
            "task": task,
            "quality_assessment": task.metadata.get("QualityAssessment"),
            "follow_up_tasks": follow_up_tasks,
        }

    # Quality is acceptable
    logger.info(f"[assess_quality_node] Quality assessment passed")
    task.status = TaskStatus.COMPLETED
    return {
        "task": task,
        "quality_assessment": task.metadata.get("QualityAssessment"),
        "follow_up_tasks": [],
    }


def should_decompose(state: OrchestrationState) -> str:
    """Routing function: determine if task should be decomposed.

    Args:
        state: Current orchestration state

    Returns:
        "decompose" if task needs decomposition, "execute" otherwise
    """
    metadata = state.get("metadata", {})
    if metadata.get("requires_decomposition"):
        logger.info("[should_decompose] Routing to decompose")
        return "decompose"

    logger.info("[should_decompose] Routing to execute")
    return "execute"


def should_follow_up(state: OrchestrationState) -> str:
    """Routing function: determine if follow-up tasks are needed.

    Args:
        state: Current orchestration state

    Returns:
        "follow_up" if follow-up tasks exist, "complete" otherwise
    """
    follow_up_tasks = state.get("follow_up_tasks", [])
    if follow_up_tasks:
        logger.info(f"[should_follow_up] Routing to follow_up ({len(follow_up_tasks)} tasks)")
        return "follow_up"

    logger.info("[should_follow_up] Routing to complete")
    return "complete"
