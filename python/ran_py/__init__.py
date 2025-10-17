"""Research Agent Network - Python/LangGraph Implementation.

This package implements a multi-agent research system using LangGraph for graph-based
orchestration. It coexists with the .NET Semantic Kernel implementation and shares
the same database.

Key Design Principles:
1. LangGraph-First: Focus on LangGraph primitives, minimize LangChain usage
2. State Over Memory: Use LangGraph State classes, not LangChain memory
3. Graphs Over Chains: Use StateGraph, not LangChain chains
4. Direct APIs: Call external APIs directly, not through LangChain wrappers
5. Database Parity: SQLAlchemy models match EF Core entities exactly
"""

__version__ = "0.1.0"
