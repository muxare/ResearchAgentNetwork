"""Database session management."""
from contextlib import asynccontextmanager, contextmanager
from typing import AsyncGenerator, Generator

from sqlalchemy import create_engine
from sqlalchemy.ext.asyncio import AsyncSession, create_async_engine
from sqlalchemy.orm import Session, sessionmaker

from ran_py.config import settings
from ran_py.db.models import Base
from ran_py.logging_config import get_logger

logger = get_logger(__name__)

# Determine database URL based on provider
if settings.database.provider == "Sqlite":
    # SQLite database URL
    DATABASE_URL = f"sqlite:///{settings.database.path}"
    ASYNC_DATABASE_URL = f"sqlite+aiosqlite:///{settings.database.path}"
else:
    # SQL Server database URL
    DATABASE_URL = settings.database.connection_string
    ASYNC_DATABASE_URL = settings.database.connection_string.replace("mssql+pyodbc://", "mssql+aioodbc://")

# Synchronous engine and session
engine = create_engine(
    DATABASE_URL,
    echo=settings.research_agent.log_prompts,  # Log SQL statements if verbose logging enabled
    pool_pre_ping=True,
)

SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# Asynchronous engine and session
async_engine = create_async_engine(
    ASYNC_DATABASE_URL,
    echo=settings.research_agent.log_prompts,
    pool_pre_ping=True,
)

AsyncSessionLocal = sessionmaker(
    async_engine,
    class_=AsyncSession,
    expire_on_commit=False,
    autocommit=False,
    autoflush=False,
)


def init_db() -> None:
    """Initialize database tables.

    Creates all tables defined in Base metadata if they don't exist.
    In production, use Alembic migrations instead.
    """
    try:
        Base.metadata.create_all(bind=engine)
        logger.info("Database tables initialized successfully")
    except Exception as e:
        logger.error(f"Failed to initialize database: {e}")
        raise


@contextmanager
def get_db() -> Generator[Session, None, None]:
    """Get a synchronous database session.

    Yields:
        Database session

    Example:
        with get_db() as db:
            task = db.query(TaskEntity).filter_by(Id=task_id).first()
    """
    db = SessionLocal()
    try:
        yield db
        db.commit()
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()


@asynccontextmanager
async def get_async_db_context() -> AsyncGenerator[AsyncSession, None]:
    """Get an asynchronous database session as a context manager.

    Yields:
        Async database session

    Example:
        async with get_async_db_context() as db:
            result = await db.execute(select(TaskEntity).filter_by(Id=task_id))
            task = result.scalar_one_or_none()
    """
    async with AsyncSessionLocal() as session:
        try:
            yield session
            await session.commit()
        except Exception:
            await session.rollback()
            raise
        finally:
            await session.close()


async def get_async_db() -> AsyncGenerator[AsyncSession, None]:
    """Get an asynchronous database session for FastAPI dependency injection.

    Yields:
        Async database session

    Example (with FastAPI):
        @app.get("/tasks")
        async def list_tasks(db: AsyncSession = Depends(get_async_db)):
            ...
    """
    async with AsyncSessionLocal() as session:
        try:
            yield session
            await session.commit()
        except Exception:
            await session.rollback()
            raise
        finally:
            await session.close()
