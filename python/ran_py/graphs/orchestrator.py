"""LangGraph orchestrator for research task processing.

This module defines the main orchestration graph that connects all agents
and manages task processing flow using LangGraph StateGraph.
"""
from typing import Any

from langgraph.graph import END, StateGraph

from ran_py.logging_config import get_logger
from ran_py.models import OrchestrationState, ResearchTask
from ran_py.graphs.nodes import (
    analyze_node,
    assess_quality_node,
    execute_node,
    should_decompose,
    should_follow_up,
)

logger = get_logger(__name__)


def create_orchestration_graph() -> StateGraph:
    """Create the main orchestration graph.

    This graph represents the research task processing pipeline:
    1. Analyze task complexity
    2. Route to decompose or execute
    3. Execute atomic task
    4. Assess quality
    5. Route to follow-up or complete

    Returns:
        Compiled StateGraph ready for execution
    """
    # Create graph with OrchestrationState
    workflow = StateGraph(OrchestrationState)

    # Add nodes (agent wrappers)
    workflow.add_node("analyze", analyze_node)
    workflow.add_node("execute", execute_node)
    workflow.add_node("assess_quality", assess_quality_node)

    # Set entry point
    workflow.set_entry_point("analyze")

    # Add conditional routing after analysis
    workflow.add_conditional_edges(
        "analyze",
        should_decompose,
        {
            "decompose": END,  # End here, orchestrator will handle subtasks
            "execute": "execute",  # Continue to execution
        },
    )

    # After execution, always assess quality
    workflow.add_edge("execute", "assess_quality")

    # Add conditional routing after quality assessment
    workflow.add_conditional_edges(
        "assess_quality",
        should_follow_up,
        {
            "follow_up": END,  # End here, orchestrator will handle follow-ups
            "complete": END,  # Task complete
        },
    )

    # Compile the graph
    return workflow.compile()


class ResearchOrchestrator:
    """Research orchestrator managing task processing with LangGraph.

    This class wraps the LangGraph workflow and provides a simple interface
    for processing research tasks.
    """

    def __init__(self) -> None:
        """Initialize orchestrator with compiled graph."""
        self.graph = create_orchestration_graph()
        logger.info("Research orchestrator initialized with LangGraph")

    async def process_task(self, task: ResearchTask) -> dict[str, Any]:
        """Process a research task through the orchestration graph.

        Args:
            task: Research task to process

        Returns:
            Final state after graph execution
        """
        logger.info(f"Processing task {task.id}: {task.description[:50]}...")

        # Initial state
        initial_state: OrchestrationState = {
            "task": task,
            "complexity_analysis": None,
            "result": None,
            "quality_assessment": None,
            "follow_up_tasks": [],
            "error": None,
            "metadata": {},
        }

        try:
            # Execute graph
            final_state = await self.graph.ainvoke(initial_state)

            # Check for errors
            if final_state.get("error"):
                logger.error(f"Task {task.id} failed: {final_state['error']}")
                return {"success": False, "error": final_state["error"], "task": task}

            # Check if decomposition is needed
            if final_state.get("metadata", {}).get("requires_decomposition"):
                subtasks = final_state["metadata"].get("subtasks", [])
                logger.info(f"Task {task.id} requires decomposition into {len(subtasks)} subtasks")
                return {
                    "success": True,
                    "requires_decomposition": True,
                    "subtasks": subtasks,
                    "task": task,
                }

            # Check if follow-up tasks are needed
            follow_up_tasks = final_state.get("follow_up_tasks", [])
            if follow_up_tasks:
                logger.info(f"Task {task.id} requires {len(follow_up_tasks)} follow-up tasks")
                return {
                    "success": True,
                    "requires_follow_up": True,
                    "follow_up_tasks": follow_up_tasks,
                    "task": task,
                    "result": final_state.get("result"),
                }

            # Task completed successfully
            logger.info(f"Task {task.id} completed successfully")
            return {
                "success": True,
                "task": task,
                "result": final_state.get("result"),
                "quality_assessment": final_state.get("quality_assessment"),
            }

        except Exception as e:
            logger.error(f"Task {task.id} processing failed with exception: {e}")
            return {"success": False, "error": str(e), "task": task}

    async def process_task_stream(self, task: ResearchTask):
        """Process task with streaming updates.

        This method yields state updates as the graph executes,
        enabling real-time progress monitoring.

        Args:
            task: Research task to process

        Yields:
            State updates at each graph node
        """
        logger.info(f"Processing task {task.id} with streaming...")

        # Initial state
        initial_state: OrchestrationState = {
            "task": task,
            "complexity_analysis": None,
            "result": None,
            "quality_assessment": None,
            "follow_up_tasks": [],
            "error": None,
            "metadata": {},
        }

        try:
            # Stream graph execution
            async for state in self.graph.astream(initial_state):
                # Yield each state update
                yield state

        except Exception as e:
            logger.error(f"Task {task.id} streaming failed: {e}")
            yield {"error": str(e), "task": task}


# Global orchestrator instance
_orchestrator: ResearchOrchestrator | None = None


def get_orchestrator() -> ResearchOrchestrator:
    """Get or create global orchestrator instance.

    Returns:
        Research orchestrator
    """
    global _orchestrator
    if _orchestrator is None:
        _orchestrator = ResearchOrchestrator()
    return _orchestrator
