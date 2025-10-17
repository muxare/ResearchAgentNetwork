"""SQLAlchemy models matching .NET EF Core entities.

IMPORTANT: Column names use PascalCase to match .NET conventions for database compatibility.
Both .NET and Python access the same SQLite database.
"""
import uuid
from datetime import datetime

from sqlalchemy import Column, DateTime, ForeignKey, Integer, String, Text
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.types import TypeDecorator, CHAR

Base = declarative_base()


class GUID(TypeDecorator):
    """Platform-independent GUID type.

    Uses PostgreSQL's UUID type, otherwise uses CHAR(36) storing as stringified hex values.
    """

    impl = CHAR
    cache_ok = True

    def load_dialect_impl(self, dialect):  # type: ignore
        if dialect.name == "postgresql":
            return dialect.type_descriptor(PG_UUID())
        else:
            return dialect.type_descriptor(CHAR(36))

    def process_bind_param(self, value, dialect):  # type: ignore
        if value is None:
            return value
        elif dialect.name == "postgresql":
            return str(value)
        else:
            if not isinstance(value, uuid.UUID):
                return str(uuid.UUID(value))
            else:
                return str(value)

    def process_result_value(self, value, dialect):  # type: ignore
        if value is None:
            return value
        else:
            if not isinstance(value, uuid.UUID):
                value = uuid.UUID(value)
            return value


class TaskEntity(Base):
    """Task entity matching .NET TaskEntity.

    Column names use PascalCase to match EF Core conventions.
    """

    __tablename__ = "Tasks"

    Id = Column(GUID(), primary_key=True, default=uuid.uuid4)
    Description = Column(String(4096), nullable=False, default="")
    Priority = Column(Integer, nullable=False, default=5)
    Status = Column(String(50), nullable=False, default="Pending")
    CreatedAtUtc = Column(DateTime, nullable=False, default=datetime.utcnow)
    UpdatedAtUtc = Column(DateTime, nullable=True)
    ParentTaskId = Column(GUID(), ForeignKey("Tasks.Id"), nullable=True)


class TaskEventEntity(Base):
    """Task event entity matching .NET TaskEventEntity.

    Column names use PascalCase to match EF Core conventions.
    """

    __tablename__ = "TaskEvents"

    Id = Column(GUID(), primary_key=True, default=uuid.uuid4)
    TaskId = Column(GUID(), ForeignKey("Tasks.Id"), nullable=False)
    EventType = Column(String(64), nullable=False, default="")
    Status = Column(String(32), nullable=False, default="")
    Message = Column(Text, nullable=True)
    TimestampUtc = Column(DateTime, nullable=False, default=datetime.utcnow)
    AgentRole = Column(String(64), nullable=True)
    DetailsJson = Column(Text, nullable=True)


class TaskReportEntity(Base):
    """Task report entity matching .NET TaskReportEntity.

    Column names use PascalCase to match EF Core conventions.
    """

    __tablename__ = "TaskReports"

    TaskId = Column(GUID(), ForeignKey("Tasks.Id"), primary_key=True)
    ReportMarkdown = Column(Text, nullable=False, default="")
    GeneratedAtUtc = Column(DateTime, nullable=False, default=datetime.utcnow)


class UserEntity(Base):
    """User entity matching .NET UserEntity.

    Column names use PascalCase to match EF Core conventions.
    """

    __tablename__ = "Users"

    Id = Column(GUID(), primary_key=True, default=uuid.uuid4)
    Username = Column(String(100), nullable=False, unique=True)
    PasswordHash = Column(String(256), nullable=False)
    Email = Column(String(256), nullable=True)
    CreatedAtUtc = Column(DateTime, nullable=False, default=datetime.utcnow)


class RefreshTokenEntity(Base):
    """Refresh token entity matching .NET RefreshTokenEntity.

    Column names use PascalCase to match EF Core conventions.
    """

    __tablename__ = "RefreshTokens"

    Id = Column(GUID(), primary_key=True, default=uuid.uuid4)
    UserId = Column(GUID(), ForeignKey("Users.Id"), nullable=False)
    Token = Column(String(512), nullable=False, unique=True)
    CreatedAtUtc = Column(DateTime, nullable=False, default=datetime.utcnow)
    ExpiresAtUtc = Column(DateTime, nullable=False)
    RevokedAtUtc = Column(DateTime, nullable=True)
    ReplacedByToken = Column(String(512), nullable=True)
