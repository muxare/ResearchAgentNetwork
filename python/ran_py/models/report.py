"""Report models matching .NET entities."""
from uuid import UUID

from pydantic import BaseModel, Field


class OutlineSection(BaseModel):
    """Report outline section matching .NET OutlineSection."""

    id: str = ""
    title: str = ""
    purpose: str = ""
    evidence_ids: list[str] = Field(default_factory=list)
    acceptance_criteria: list[str] = Field(default_factory=list)


class ReportOutline(BaseModel):
    """Report outline matching .NET ReportOutline."""

    root_task_id: UUID
    sections: list[OutlineSection] = Field(default_factory=list)


class ReportSectionDraft(BaseModel):
    """Report section draft matching .NET ReportSectionDraft."""

    section_id: str = ""
    title: str = ""
    content_md: str = ""
    citations: list[str] = Field(default_factory=list)
    facts_used: list[str] = Field(default_factory=list)
    confidence: float = 0.0
