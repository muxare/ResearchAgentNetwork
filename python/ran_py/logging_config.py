"""Structured logging configuration."""
import logging
import sys
from typing import Any

# Configure structured logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)8s] %(name)s - %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
    handlers=[logging.StreamHandler(sys.stdout)],
)


def get_logger(name: str) -> logging.Logger:
    """Get a logger with the specified name.

    Args:
        name: Logger name (typically __name__)

    Returns:
        Configured logger instance
    """
    return logging.getLogger(name)


def log_prompt(logger: logging.Logger, role: str, prompt: str, response: str | None = None) -> None:
    """Log LLM prompt and response if enabled.

    Args:
        logger: Logger instance
        role: Agent role/name
        prompt: The prompt sent to the LLM
        response: The response from the LLM (optional)
    """
    from ran_py.config import settings

    if not settings.research_agent.log_prompts:
        return

    logger.debug(f"[{role}] Prompt:\n{prompt}")
    if response:
        logger.debug(f"[{role}] Response:\n{response}")


def log_structured(logger: logging.Logger, level: int, message: str, **kwargs: Any) -> None:
    """Log a structured message with additional context.

    Args:
        logger: Logger instance
        level: Logging level (e.g., logging.INFO)
        message: Main log message
        **kwargs: Additional structured fields
    """
    extra_fields = " ".join(f"{k}={v}" for k, v in kwargs.items())
    logger.log(level, f"{message} | {extra_fields}" if extra_fields else message)
