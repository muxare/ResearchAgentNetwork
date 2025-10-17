"""Configuration management using Pydantic Settings."""
from typing import Literal

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class DatabaseSettings(BaseSettings):
    """Database configuration."""

    provider: Literal["Sqlite", "SqlServer"] = "Sqlite"
    path: str = Field(default="../data/ran.db", alias="DATABASE_PATH")
    connection_string: str = Field(default="", alias="DATABASE_CONNECTION_STRING")


class OllamaSettings(BaseSettings):
    """Ollama LLM configuration."""

    model_id: str = Field(default="llama3.1:latest", alias="OLLAMA_MODEL_ID")
    endpoint: str = Field(default="http://localhost:11434", alias="OLLAMA_BASE_URL")
    embedding_model_id: str = Field(default="nomic-embed-text", alias="OLLAMA_EMBEDDING_MODEL_ID")


class OpenAISettings(BaseSettings):
    """OpenAI configuration."""

    model_id: str = Field(default="gpt-4o-mini", alias="OPENAI_MODEL_ID")
    api_key: str = Field(default="", alias="OPENAI_API_KEY")
    embedding_model_id: str = Field(default="text-embedding-3-small", alias="OPENAI_EMBEDDING_MODEL_ID")


class ResearchAgentSettings(BaseSettings):
    """Research agent orchestration settings."""

    max_concurrency: int = Field(default=5, alias="MAX_CONCURRENCY")
    default_priority: int = Field(default=5, alias="DEFAULT_PRIORITY")
    task_timeout_minutes: int = Field(default=30, alias="TASK_TIMEOUT_MINUTES")
    max_decomposition_depth: int = Field(default=2, alias="MAX_DECOMPOSITION_DEPTH")
    max_retries: int = Field(default=1, alias="MAX_RETRIES")
    log_prompts: bool = Field(default=False, alias="LOG_PROMPTS")
    enable_web_search: bool = Field(default=False, alias="ENABLE_WEB_SEARCH")


class MergingSettings(BaseSettings):
    """Task merging configuration."""

    pending_threshold: float = Field(default=0.9, alias="MERGING_PENDING_THRESHOLD")
    completed_threshold: float = Field(default=0.95, alias="MERGING_COMPLETED_THRESHOLD")


class RAGSettings(BaseSettings):
    """Retrieval-Augmented Generation settings."""

    store_min_confidence: float = Field(default=0.6, alias="RAG_STORE_MIN_CONFIDENCE")
    duplicate_threshold: float = Field(default=0.98, alias="RAG_DUPLICATE_THRESHOLD")


class VectorDbSettings(BaseSettings):
    """Vector database configuration."""

    provider: Literal["Qdrant", "None"] = Field(default="Qdrant", alias="VECTOR_DB_PROVIDER")
    endpoint: str = Field(default="http://localhost:6333", alias="QDRANT_URL")
    collection_prefix: str = Field(default="ran", alias="QDRANT_COLLECTION_PREFIX")
    top_k: int = Field(default=5, alias="QDRANT_TOP_K")


class TavilySettings(BaseSettings):
    """Tavily web search configuration."""

    api_key: str = Field(default="", alias="TAVILY_API_KEY")


class WebSearchSettings(BaseSettings):
    """Web search configuration."""

    provider: Literal["TavilyApi", "None"] = Field(default="None", alias="WEB_SEARCH_PROVIDER")
    tavily: TavilySettings = Field(default_factory=TavilySettings)
    allowlist: str = Field(default="", alias="WEB_SEARCH_ALLOWLIST")  # Comma-separated domains
    rpm: int = Field(default=30, alias="WEB_SEARCH_RPM")
    min_interval_ms: int = Field(default=500, alias="WEB_SEARCH_MIN_INTERVAL_MS")


class JwtSettings(BaseSettings):
    """JWT authentication configuration."""

    secret_key: str = Field(default="change-me-in-production", alias="JWT_SECRET_KEY")
    algorithm: str = Field(default="HS256", alias="JWT_ALGORITHM")
    access_token_expire_minutes: int = Field(default=30, alias="JWT_ACCESS_TOKEN_EXPIRE_MINUTES")
    refresh_token_expire_days: int = Field(default=7, alias="JWT_REFRESH_TOKEN_EXPIRE_DAYS")


class APISettings(BaseSettings):
    """API server configuration."""

    host: str = Field(default="0.0.0.0", alias="API_HOST")
    port: int = Field(default=8090, alias="API_PORT")
    reload: bool = Field(default=True, alias="API_RELOAD")


class Settings(BaseSettings):
    """Main application settings."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

    # AI Provider
    ai_provider: Literal["Ollama", "OpenAI", "AzureOpenAI"] = Field(default="Ollama", alias="AI_PROVIDER")

    # Component settings
    database: DatabaseSettings = Field(default_factory=DatabaseSettings)
    ollama: OllamaSettings = Field(default_factory=OllamaSettings)
    openai: OpenAISettings = Field(default_factory=OpenAISettings)
    research_agent: ResearchAgentSettings = Field(default_factory=ResearchAgentSettings)
    merging: MergingSettings = Field(default_factory=MergingSettings)
    rag: RAGSettings = Field(default_factory=RAGSettings)
    vector_db: VectorDbSettings = Field(default_factory=VectorDbSettings)
    web_search: WebSearchSettings = Field(default_factory=WebSearchSettings)
    jwt: JwtSettings = Field(default_factory=JwtSettings)
    api: APISettings = Field(default_factory=APISettings)


# Global settings instance
settings = Settings()
