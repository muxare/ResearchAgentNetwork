"""Structured output helper matching .NET KernelExtensions.

This module provides structured LLM outputs using Pydantic models,
similar to .NET's WithStructuredOutput<T>() method.
"""
import json
import re
from typing import Any, TypeVar

from langchain_core.language_models import BaseChatModel
from langchain_core.messages import HumanMessage, SystemMessage
from pydantic import BaseModel

from ran_py.logging_config import get_logger

logger = get_logger(__name__)

T = TypeVar("T", bound=BaseModel)


class StructuredOutputResult:
    """Result wrapper for structured output operations.

    Matches .NET StructuredOutputResult<T> pattern.
    """

    def __init__(self, is_success: bool, value: Any | None = None, error_message: str | None = None):
        """Initialize result.

        Args:
            is_success: Whether operation succeeded
            value: The parsed value (if successful)
            error_message: Error message (if failed)
        """
        self.is_success = is_success
        self.value = value
        self.error_message = error_message

    @staticmethod
    def success(value: T) -> "StructuredOutputResult":
        """Create success result."""
        return StructuredOutputResult(is_success=True, value=value)

    @staticmethod
    def failure(error_message: str) -> "StructuredOutputResult":
        """Create failure result."""
        return StructuredOutputResult(is_success=False, error_message=error_message)

    def get_value_or_throw(self) -> T:
        """Get value or raise exception if failed."""
        if not self.is_success:
            raise ValueError(f"Structured output failed: {self.error_message}")
        return self.value  # type: ignore


def _extract_json_payload(text: str) -> str:
    """Extract JSON payload from LLM response.

    Handles common LLM response formats:
    - Code fences (```json ... ```)
    - Language hints (json\n{...})
    - Smart quotes
    - Triple quotes

    Args:
        text: Raw LLM response

    Returns:
        Extracted JSON string
    """
    if not text or not text.strip():
        return "{}"

    first = text.strip()

    # Remove code fences
    if first.startswith("```"):
        idx = first.find("\n")
        if idx > 0:
            first = first[idx + 1 :]
        end = first.rfind("```")
        if end > 0:
            first = first[:end]

    # Remove language hints
    if first.lower().startswith("json"):
        idx = first.find("\n")
        if idx > 0:
            first = first[idx + 1 :]

    # Normalize smart quotes
    first = first.replace("\u201c", '"').replace("\u201d", '"')
    first = first.replace("\u2018", "'").replace("\u2019", "'")

    # Replace triple quotes
    first = first.replace('"""', '"')

    first = first.strip()
    if not first:
        return "{}"

    # If starts with valid JSON, return as-is
    if first.startswith("[") or first.startswith("{"):
        return first

    # Find first JSON object or array
    start_array = first.find("[")
    start_object = first.find("{")

    if start_array >= 0 and (start_object < 0 or start_array < start_object):
        start = start_array
        open_char = "["
        close_char = "]"
    elif start_object >= 0:
        start = start_object
        open_char = "{"
        close_char = "}"
    else:
        return text

    # Find matching close bracket
    depth = 0
    for i in range(start, len(first)):
        if first[i] == open_char:
            depth += 1
        elif first[i] == close_char:
            depth -= 1
        if depth == 0 and i > start:
            return first[start : i + 1]

    return first[start:]


def _repair_invalid_string_literals(json_str: str) -> str:
    """Repair invalid string literals in JSON.

    Escapes unescaped newlines, tabs, etc. within string values.

    Args:
        json_str: JSON string to repair

    Returns:
        Repaired JSON string
    """
    result = []
    in_string = False
    escape_next = False

    for c in json_str:
        if escape_next:
            result.append(c)
            escape_next = False
            continue

        if c == "\\":
            result.append(c)
            if in_string:
                escape_next = True
            continue

        if c == '"':
            in_string = not in_string
            result.append(c)
            continue

        if in_string:
            if c == "\r" or c == "\n":
                result.append("\\n")
                continue
            if c == "\t":
                result.append("\\t")
                continue

        result.append(c)

    return "".join(result)


async def get_structured_output(
    llm: BaseChatModel,
    prompt: str,
    response_model: type[T],
    system_prompt: str | None = None,
    log_prompts: bool = False,
) -> T:
    """Get structured output from LLM matching a Pydantic model.

    This function mirrors .NET's KernelExtensions.WithStructuredOutput<T>().

    Args:
        llm: LangChain chat model
        prompt: User prompt
        response_model: Pydantic model class for response
        system_prompt: Optional system prompt
        log_prompts: Whether to log prompts and responses

    Returns:
        Parsed response matching response_model

    Raises:
        ValueError: If response cannot be parsed into response_model
    """
    # Generate JSON schema from Pydantic model
    schema = response_model.model_json_schema()
    schema_str = json.dumps(schema, indent=2)

    # Enhance prompt with schema guidance
    enhanced_prompt = f"""{prompt}

IMPORTANT: You must respond with valid JSON that conforms to this exact schema:

{schema_str}

Ensure your response is valid JSON and matches the schema structure exactly. Do not include any text before or after the JSON."""

    if log_prompts:
        logger.info(f"LLM Prompt:\n{enhanced_prompt}")

    # Build messages
    messages = []
    if system_prompt:
        messages.append(SystemMessage(content=system_prompt))
    messages.append(HumanMessage(content=enhanced_prompt))

    # Invoke LLM
    response = await llm.ainvoke(messages)
    raw = response.content

    if log_prompts:
        logger.info(f"LLM Response:\n{raw}")

    # Extract and parse JSON
    json_response = _extract_json_payload(str(raw))

    try:
        # Try direct parsing
        return response_model.model_validate_json(json_response)
    except Exception as e:
        # Try repairing and parsing again
        try:
            repaired = _repair_invalid_string_literals(json_response)
            return response_model.model_validate_json(repaired)
        except Exception:
            # Both attempts failed
            raise ValueError(
                f"Failed to parse LLM response as {response_model.__name__}. "
                f"Response: {raw}. "
                f"Error: {str(e)}"
            ) from e


async def get_structured_output_safe(
    llm: BaseChatModel,
    prompt: str,
    response_model: type[T],
    system_prompt: str | None = None,
    log_prompts: bool = False,
) -> StructuredOutputResult:
    """Get structured output with error handling.

    Matches .NET's WithStructuredOutputSafe<T>() method.

    Args:
        llm: LangChain chat model
        prompt: User prompt
        response_model: Pydantic model class for response
        system_prompt: Optional system prompt
        log_prompts: Whether to log prompts and responses

    Returns:
        StructuredOutputResult with success/failure info
    """
    try:
        result = await get_structured_output(llm, prompt, response_model, system_prompt, log_prompts)
        return StructuredOutputResult.success(result)
    except Exception as e:
        logger.error(f"Structured output failed: {e}")
        return StructuredOutputResult.failure(str(e))


async def get_structured_output_retry(
    llm: BaseChatModel,
    prompt: str,
    response_model: type[T],
    max_retries: int = 3,
    system_prompt: str | None = None,
    log_prompts: bool = False,
) -> T:
    """Get structured output with automatic retries.

    Matches .NET's WithStructuredOutputRetry<T>() method.

    Args:
        llm: LangChain chat model
        prompt: User prompt
        response_model: Pydantic model class for response
        max_retries: Maximum number of retry attempts
        system_prompt: Optional system prompt
        log_prompts: Whether to log prompts and responses

    Returns:
        Parsed response matching response_model

    Raises:
        ValueError: If all retry attempts fail
    """
    import asyncio

    last_error = None
    current_prompt = prompt

    for attempt in range(1, max_retries + 1):
        try:
            return await get_structured_output(llm, current_prompt, response_model, system_prompt, log_prompts)
        except Exception as e:
            last_error = e
            if attempt < max_retries:
                logger.warning(f"Structured output attempt {attempt} failed: {e}. Retrying...")
                # Add error feedback to prompt for retry
                current_prompt = f"""{prompt}

Previous attempt failed with error: {str(e)}

Please ensure your response is valid JSON and try again."""
                # Exponential backoff
                delay = 2 ** (attempt - 1)
                await asyncio.sleep(delay)

    raise ValueError(
        f"Failed to get structured output after {max_retries} attempts. "
        f"Last error: {str(last_error)}"
    ) from last_error
