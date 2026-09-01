# TDK-Engineering-Showcase
AiService:
Secure NL2SQL (Natural Language to SQL) Engine

This module bridges the gap between non-technical fleet managers and the SQL database, allowing users to ask natural language questions (e.g., "Which vehicles have failed inspections this month?") and generating execution-ready SQL.

Key Architecture & Security Features:

Defense in Depth: Employs a two-tier security model. The application layer filters for read-only intent, while the database layer executes the AI-generated queries using a strictly limited, Read-Only SQL credentials (SqlConnection), mitigating any AI-hallucinated SQL Injection risks.

Domain-Aware Prompting: The system prompt is dynamically injected with the database schema and explicit business logic translations (e.g., mapping logical flags like "SVillogo" to "yellow rotating beacon").

Dynamic Result Mapping: Uses raw ADO.NET SqlDataReader to map unpredictable query results into generic Dictionary<string, object> lists for seamless frontend serialization.

Modern AI SDK: Built with the latest Azure OpenAI v2 SDK, enforcing zero temperature for deterministic, hallucination-free code generation.
