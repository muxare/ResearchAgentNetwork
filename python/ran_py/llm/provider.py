"""LLM provider abstraction.

Provides unified interface for different LLM providers (Ollama, OpenAI, etc.),
matching .NET's IAIProvider pattern.
"""
from abc import ABC, abstractmethod
from typing import Any

from langchain_core.language_models import BaseChatModel
from langchain_ollama import ChatOllama

from ran_py.config import settings
from ran_py.logging_config import get_logger

logger = get_logger(__name__)


class LLMProvider(ABC):
    """Abstract LLM provider interface."""

    @abstractmethod
    def get_chat_model(self) -> BaseChatModel:
        """Get chat model for LLM interactions.

        Returns:
            LangChain chat model instance
        """
        pass

    @abstractmethod
    def get_embedding_model(self) -> Any:  # noqa: F821
        """Get embedding model for vector operations.

        Returns:
            Embedding model instance
        """
        pass


class OllamaProvider(LLMProvider):
    """Ollama LLM provider.

    Uses langchain-ollama for local LLM access.
    """

    def __init__(self) -> None:
        """Initialize Ollama provider from settings."""
        self.model_id = settings.ollama.model_id
        self.base_url = settings.ollama.endpoint
        self.embedding_model_id = settings.ollama.embedding_model_id

        logger.info(
            f"Initialized OllamaProvider: model={self.model_id}, "
            f"endpoint={self.base_url}, "
            f"embedding={self.embedding_model_id}"
        )

    def get_chat_model(self) -> BaseChatModel:
        """Get Ollama chat model.

        Returns:
            ChatOllama instance configured from settings
        """
        return ChatOllama(
            model=self.model_id,
            base_url=self.base_url,
            temperature=0.7,  # Balanced creativity
            num_predict=2048,  # Max tokens
        )

    def get_embedding_model(self) -> ChatOllama:
        """Get Ollama embedding model.

        Returns:
            ChatOllama instance for embeddings
        """
        return ChatOllama(
            model=self.embedding_model_id,
            base_url=self.base_url,
        )


class OpenAIProvider(LLMProvider):
    """OpenAI LLM provider.

    Uses langchain-openai for OpenAI API access.
    """

    def __init__(self) -> None:
        """Initialize OpenAI provider from settings."""
        self.model_id = settings.openai.model_id
        self.api_key = settings.openai.api_key
        self.embedding_model_id = settings.openai.embedding_model_id

        if not self.api_key:
            raise ValueError("OpenAI API key not configured. Set OPENAI_API_KEY environment variable.")

        logger.info(f"Initialized OpenAIProvider: model={self.model_id}, embedding={self.embedding_model_id}")

    def get_chat_model(self) -> BaseChatModel:
        """Get OpenAI chat model.

        Returns:
            ChatOpenAI instance configured from settings
        """
        from langchain_openai import ChatOpenAI

        return ChatOpenAI(
            model=self.model_id,
            api_key=self.api_key,
            temperature=0.7,
            max_tokens=2048,
        )

    def get_embedding_model(self) -> Any:  # noqa: F821
        """Get OpenAI embedding model.

        Returns:
            OpenAIEmbeddings instance
        """
        from langchain_openai import OpenAIEmbeddings

        return OpenAIEmbeddings(
            model=self.embedding_model_id,
            api_key=self.api_key,
        )


def create_llm_provider() -> LLMProvider:
    """Factory function to create LLM provider based on configuration.

    Matches .NET's AIProviderFactory.CreateProvider() pattern.

    Returns:
        LLM provider instance

    Raises:
        ValueError: If provider is not supported
    """
    provider_name = settings.ai_provider

    if provider_name == "Ollama":
        return OllamaProvider()
    elif provider_name == "OpenAI":
        return OpenAIProvider()
    else:
        raise ValueError(
            f"Unsupported AI provider: {provider_name}. "
            f"Supported providers: Ollama, OpenAI"
        )


# Global provider instance
_provider: LLMProvider | None = None


def get_llm_provider() -> LLMProvider:
    """Get or create global LLM provider instance.

    Returns:
        LLM provider instance
    """
    global _provider
    if _provider is None:
        _provider = create_llm_provider()
    return _provider
