"""FastAPI application for Research Agent Network Python implementation.

This API provides parity with the .NET endpoints for Phase 2 priority features:
- POST /api/tasks - Submit research tasks
- GET /api/tasks/{id} - Get task status
- GET /api/events - SSE stream for real-time updates
"""
import asyncio
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from ran_py.api.routes import events, tasks
from ran_py.config import settings
from ran_py.db.session import init_db
from ran_py.logging_config import get_logger

logger = get_logger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager.

    Handles startup and shutdown logic.
    """
    # Startup
    logger.info("Starting Research Agent Network Python service...")
    logger.info(f"AI Provider: {settings.ai_provider}")
    logger.info(f"Database: {settings.database.provider}")
    logger.info(f"Vector DB: {settings.vector_db.provider}")

    # Initialize database
    try:
        init_db()
        logger.info("Database initialized successfully")
    except Exception as e:
        logger.error(f"Failed to initialize database: {e}")

    yield

    # Shutdown
    logger.info("Shutting down Research Agent Network Python service...")


# Create FastAPI app
app = FastAPI(
    title="Research Agent Network - Python/LangGraph",
    description="Multi-agent research system using LangGraph orchestration",
    version="0.1.0",
    lifespan=lifespan,
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure appropriately for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# Exception handler
@app.exception_handler(Exception)
async def global_exception_handler(request, exc):
    """Global exception handler."""
    logger.error(f"Unhandled exception: {exc}", exc_info=True)
    return JSONResponse(
        status_code=500,
        content={"error": "Internal server error", "detail": str(exc)},
    )


# Health check
@app.get("/health")
async def health_check():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "service": "research-agent-network-python",
        "ai_provider": settings.ai_provider,
        "version": "0.1.0",
    }


# Include routers
app.include_router(tasks.router, prefix="/api", tags=["tasks"])
app.include_router(events.router, prefix="/api", tags=["events"])


# Root endpoint
@app.get("/")
async def root():
    """Root endpoint with API information."""
    return {
        "service": "Research Agent Network - Python/LangGraph",
        "version": "0.1.0",
        "docs": "/docs",
        "health": "/health",
        "endpoints": {
            "tasks": "/api/tasks",
            "events": "/api/events (SSE)",
        },
    }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "ran_py.api.app:app",
        host=settings.api.host,
        port=settings.api.port,
        reload=settings.api.reload,
        log_level="info",
    )
