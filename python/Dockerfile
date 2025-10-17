# Multi-stage Dockerfile for Python/LangGraph implementation

# Stage 1: Base image with dependencies
FROM python:3.11-slim AS base

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    gcc \
    g++ \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements first for layer caching
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Stage 2: Development image
FROM base AS development

# Copy application code
COPY ran_py/ ./ran_py/
COPY tests/ ./tests/
COPY pytest.ini .

# Install development dependencies
RUN pip install --no-cache-dir pytest pytest-asyncio pytest-cov ruff mypy

# Expose port
EXPOSE 8090

# Run with hot reload
CMD ["uvicorn", "ran_py.api.app:app", "--host", "0.0.0.0", "--port", "8090", "--reload"]

# Stage 3: Production image
FROM base AS production

# Copy only application code (no tests)
COPY ran_py/ ./ran_py/

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Expose port
EXPOSE 8090

# Run with optimized settings
CMD ["uvicorn", "ran_py.api.app:app", "--host", "0.0.0.0", "--port", "8090", "--workers", "4"]
