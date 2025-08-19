# Research Agent Network - DevOps Execution Plan

## 📊 Current Implementation Status

### ✅ Fully Implemented & Working

#### Core Multi-Agent Architecture
- [x] **Complete Agent System**: All 15 agents implemented and functional
  - TaskAnalyzerAgent, TaskMergerAgent, ExecutorAgent, AggregatorAgent
  - QualityAssessmentAgent, CitationManagerAgent, KnowledgeCuratorAgent
  - MemoryRouterAgent, RetrievalDecisionAgent, ReportOutlineAgent
  - SectionWriterAgent, WebSearchAgent, TaskAnalyzerAgent, TaskMergerAgent
- [x] **Research Orchestrator**: Full task lifecycle management with concurrency control
- [x] **Domain Models**: Complete data structures for tasks, results, citations, and events
- [x] **Semantic Kernel Integration**: Native SK functionality with structured output handling

#### AI Provider Infrastructure
- [x] **Multi-Provider Support**: Ollama, OpenAI, Azure OpenAI via AIProviderFactory
- [x] **Ollama Integration**: Fully functional with local models
- [x] **Configuration Management**: Environment variables and appsettings.json support

#### Vector Database & Semantic Memory
- [x] **Qdrant Integration**: Full vector store implementation with semantic memory
- [x] **Embedding Service**: Functional embedding generation and storage
- [x] **Memory Service**: Complete semantic memory indexing and retrieval

#### Web Search & Ingestion
- [x] **Web Search Agent**: Tavily integration with provenance metadata
- [x] **Ingestion Pipeline**: Web results chunking, embedding, and storage
- [x] **Provenance Tracking**: URL, title, and chunk metadata preservation

#### Web API & Backend
- [x] **ASP.NET Core Web API**: Complete REST endpoints for task management
- [x] **Server-Sent Events**: Real-time task updates via SSE
- [x] **Task Management**: Full CRUD operations with status tracking
- [x] **Progress Monitoring**: Real-time progress updates and event streaming

#### Frontend UI (SvelteKit)
- [x] **Modern SvelteKit UI**: Complete Kanban board interface
- [x] **Real-time Updates**: Live task status updates via SSE
- [x] **Task Management**: Create, view, and monitor research tasks
- [x] **Performance Optimized**: Efficient rendering with proper state management
- [x] **Responsive Design**: Tailwind CSS with modern UI components

#### Testing Infrastructure
- [x] **Unit Tests**: Comprehensive test coverage for core components
- [x] **LLM Integration Tests**: Ollama-based integration testing
- [x] **Test Helpers**: LLMTestHelper and test configuration

### 🔄 Partially Implemented

#### Vector Database Features
- [x] **Qdrant Backend**: Fully implemented
- [x] **Advanced Similarity**: Semantic similarity detection needs refinement
- [x] **Task Deduplication**: Similarity-based task merging needs enhancement

#### Web Search Features
- [x] **Basic Web Search**: Tavily integration working
- [x] **Query Planning**: Advanced query planning for web search
- [x] **Rate Limiting**: Web search rate limiting and allowlisting

### ❌ Not Yet Implemented

#### Database Persistence
- [x] **Entity Framework**: Database integration for task persistence
- [x] **Task History**: Long-term task storage and audit trails
- [x] **User Management**: Persistence + JWT auth endpoints (register, login, refresh, logout)
- [ ] **Authorization**: Role-based policies and admin route protection
- [ ] **UI Integration**: Login flow, token storage, and auth header wiring

#### Advanced Features
- [ ] **Plugin System (Semantic Kernel–aligned)**
  - Use SK plugins as the extensibility unit (native functions and OpenAPI plugins) with function-calling
  - Import patterns: `ImportPluginFromType()` for local code, `ImportPluginFromOpenApiAsync()` for HTTP services
  - Author rich function descriptions to improve planner/function selection; prefer narrow, well-typed inputs
  - Expose existing agents (e.g., web search, memory ops) as plugins and allow orchestrator to invoke via function choice
  - Acceptance: POC with 2 plugins (InternalTasksPlugin, WebSearchPlugin), orchestrator can auto-select and invoke
- [ ] **Result Caching (Semantic + Vector)**
  - Short-term semantic answer cache keyed by normalized prompt + settings; TTL + LRU policy
  - Use SK Vector Store abstractions for RAG-level dedupe/near-duplicate checks before re-executing tasks
  - Optionally evaluate Kernel Memory service for higher-level ingestion/citation workflows where useful
  - Acceptance: 20–40% hit rate on repeated requests; cache bypass toggle; correctness checks vs cold path
- [ ] **Multi-Tenant Support**
  - Persistence: per-tenant scoping via `TenantId` columns and EF global query filters
  - Vector memory: collection/namespace prefix per tenant (e.g., `ran_<tenant>_<dims>`), plus tag filters
  - AuthZ: map JWT claims → tenant and roles; enforce on repositories and API endpoints
  - Acceptance: cross-tenant isolation verified by DB queries and vector retrieval tests
- [ ] **Export Capabilities**
  - Start with robust Markdown export (already in reports); add PDF rendering server-side
  - Keep HTML export optional; ensure citations survive format conversion
  - Acceptance: deterministic PDF export for a sample report with images/links, <= 2s render on average
- [ ] **Agent Framework Alignment (SK)**
  - Evaluate SK Agent Framework (e.g., `ChatCompletionAgent`) for future orchestration and tool-calling parity
  - Map current orchestrator events to SK agent messages; instrument intermediate tool calls
  - Acceptance: spike that mirrors one task flow using an SK agent + plugins with equivalent results

## 🎯 DevOps Execution Plan

### Phase 1: Stabilization & Testing (Week 1-2)
**Goal**: Ensure current implementation is production-ready

#### 1.1 Testing & Quality Assurance
- [ ] **Test Coverage Audit**: Ensure >90% code coverage
- [ ] **Performance Testing**: Load testing for concurrent task processing
- [ ] **Integration Testing**: End-to-end workflow validation
- [ ] **Error Handling**: Comprehensive error scenario testing

#### 1.2 Documentation & Deployment
- [ ] **Deployment Guide**: Production deployment instructions
- [ ] **API Documentation**: OpenAPI/Swagger documentation
- [ ] **User Manual**: End-user documentation and tutorials
- [ ] **Monitoring Setup**: Application performance monitoring

### Phase 2: Production Readiness (Week 3-4)
**Goal**: Prepare for production deployment

#### 2.1 Infrastructure & Security
- [ ] **Security Hardening**: Input validation, output sanitization
- [ ] **Configuration Management**: Secure secrets management
- [ ] **Logging & Monitoring**: Structured logging and metrics
- [ ] **Health Checks**: Application health monitoring endpoints

#### 2.2 Performance & Scalability
- [ ] **Performance Optimization**: Task processing optimization
- [ ] **Resource Management**: Memory and CPU usage optimization
- [ ] **Scalability Testing**: Horizontal scaling validation
- [ ] **Caching Strategy**: Result caching implementation

### Phase 3: Advanced Features (Month 2)
**Goal**: Implement advanced research capabilities

#### 3.1 Enhanced Search & Retrieval
- [ ] **Advanced Similarity**: Improve semantic similarity detection
- [ ] **Query Planning**: Intelligent web search query generation
- [ ] **Result Ranking**: Better result relevance scoring (later todo)
- [ ] **Context Enrichment**: Enhanced context building for tasks

#### 3.1.1 Configurable Retrieval Parameters (Follow-up)
- [ ] Externalize MMR parameters to configuration
  - `VectorDb:Retrieval:MMR:Alpha` (default 0.7)
  - `VectorDb:Retrieval:MMR:ThresholdRatio` (default 0.7)

#### 3.2 User Experience
- [ ] **Advanced UI Features**: Enhanced task visualization
- [ ] **Collaboration Tools**: Multi-user task sharing
- [ ] **Template System**: Predefined research templates
- [ ] **Progress Tracking**: Enhanced progress visualization

### Phase 4: Enterprise Features (Month 3)
**Goal**: Add enterprise-grade capabilities

#### 4.1 Data Management
- [x] **Database Integration**: Entity Framework implementation
- [ ] **Data Migration**: Task data persistence
- [ ] **Backup & Recovery**: Data backup strategies
- [ ] **Audit Logging**: Comprehensive audit trails

#### 4.2 Enterprise Features
- [x] **User Authentication**: Basic JWT issuance and refresh endpoints
- [ ] **Role-Based Access**: Permission management
- [ ] **Multi-Tenancy**: User isolation
- [ ] **Compliance**: GDPR and regulatory compliance

### Phase 5: Innovation & Growth (Month 4+)
**Goal**: Future-proof the platform

#### 5.1 AI Enhancement
- [ ] **Learning System**: Pattern recognition from past research
- [ ] **Adaptive Strategies**: Dynamic task decomposition
- [ ] **Predictive Analytics**: Task completion time prediction
- [ ] **Quality Improvement**: Continuous learning from results

#### 5.2 Platform Extensibility
- [ ] **Plugin Architecture**: Custom agent development
- [ ] **API Marketplace**: Third-party integrations
- [ ] **Custom Workflows**: User-defined research processes
- [ ] **Integration Hub**: External tool connections

## 🚀 Immediate Next Steps (This Week)

### High Priority
1. **Auto-Trigger Finalization Pipeline (Root Completion)**: When a root task completes (either directly or via aggregated subtasks), automatically kick off the finalization pipeline
   - Outline: generate report outline via `ReportOutlineAgent`
   - Section Drafting: produce section drafts via `SectionWriterAgent`
   - Fact Checking: verify claims and provenance via `QualityAssessmentAgent`
   - Citation Normalization: dedupe/format citations via `CitationManagerAgent`
   - Visibility: spawn these as system subtasks so they appear as task cards for transparency
   - Acceptance: on root completion, the four steps run in order with retries; progress visible via SSE; persisted `TaskReport` is produced
2. **Publish Final Report (In-App Viewer + Download)**: Make the written final report available to read in the app and downloadable in a fitting format
   - Backend: `GET /api/reports/{taskId}` (Markdown/JSON), `GET /api/reports/{taskId}/download?format=md` (PDF later)
   - Frontend (SvelteKit): route `/tasks/{id}/report` renders Markdown with a clean Tailwind layout; includes download button
   - Realtime: show a “Finalizing…” state until the report is ready; refresh automatically when finalization completes
   - Acceptance: after finalization, users can open the report page, read the full Markdown, and download the `.md` file; PDF export is tracked separately
3. **Complete Testing Suite**: Finalize unit and integration tests
4. **Performance Validation**: Test with realistic task loads
5. **Documentation Review**: Update all documentation for accuracy
6. **Deployment Preparation**: Create production deployment scripts

### Medium Priority
1. **Error Handling**: Enhance error handling and user feedback
2. **Monitoring Setup**: Implement basic application monitoring
3. **Security Review**: Security audit of current implementation
4. **User Experience**: Polish UI/UX based on testing feedback

## 🔧 User Experience Insights & System Improvements

### Task Management & Workflow Enhancements

#### Priority System Improvements
- [ ] **Depth-First Priority**: Implement depth-first priority assignment for subtasks
  - **Technical Implementation**:
    - Add `CalculatePriority(Task task, int depth, TaskContext context)` method to orchestrator
    - Implement priority formula: `BasePriority + (MaxDepth - depth) * DepthMultiplier`
    - Add `PriorityAssignmentStrategy` enum: `DepthFirst`, `BreadthFirst`, `Custom`
    - Store priority calculation logic in `IPriorityCalculator` interface
- [ ] **Aggregation Priority Boost**: Give aggregation tasks higher priority to complete early
  - **Technical Implementation**:
    - Add `AggregationPriorityBoost` configuration setting (default: +2)
    - Implement priority boost in `AggregatorAgent` when parent task is ready for aggregation
    - Add `IsAggregationTask(Task task)` helper method
- [ ] **Dynamic Priority Adjustment**: Adjust priorities based on task dependencies and completion status
  - **Technical Implementation**:
    - Create `PriorityAdjustmentService` that monitors task dependencies
    - Implement `AdjustPriorities()` method called periodically or on dependency changes
    - Add priority adjustment rules: `BlockedTasks` get lower priority, `ReadyTasks` get higher priority

#### Task Evaluation & Decomposition
- [ ] **Sophisticated Evaluation Function**: Replace hard-coded split limits with intelligent analysis
  - **Domain Complexity Analysis**: Medical, legal, technical, creative task classification
  - **Human Involvement Detection**: Identify tasks requiring human input, approval, or external data
  - **Resource Requirement Assessment**: Analyze needs for external APIs, databases, or expert input
  - **Time Complexity Estimation**: LLM-based time assessment for better decomposition decisions
  - **Out-of-the-box Potential**: Detect creative tasks that benefit from unexpected approaches
  - **Learning from History**: Analyze past similar tasks to predict optimal decomposition
  - **Technical Implementation**:
    - Create `ITaskComplexityAnalyzer` interface with `AnalyzeComplexity(TaskDescription)` method
    - Implement `MultiDimensionalComplexityAnalyzer` using LLM for domain classification
    - Add complexity factors: `DomainComplexity`, `HumanInvolvement`, `ResourceRequirements`, `TimeEstimate`
    - Create `ComplexityScore` model with weighted factors and confidence levels
    - Implement `DecompositionStrategy` enum: `DirectExecution`, `SplitIntoSubtasks`, `RequireApproval`
    - Add `ShouldDecompose(ComplexityScore, TaskContext)` logic with dynamic thresholds
    - Store complexity analysis results in `TaskMetadata` for future learning
- [ ] **Dynamic Split Thresholds**: Adjust decomposition limits based on task type and available resources
  - **Technical Implementation**:
    - Create `DecompositionThresholds` configuration with per-domain limits
    - Implement `ThresholdAdjuster` that considers system load, available agents, and task history
    - Add adaptive thresholds: `BaseThreshold * DomainMultiplier * LoadMultiplier * HistoryMultiplier`

#### Task Lifecycle & Recovery
- [ ] **Smart Retry Logic**: Resume from failure point instead of starting over completely
  - **Result Preservation**: Leverage existing results and partial progress when retrying
  - **Failure Point Detection**: Identify exactly where the process stopped and resume from there
  - **Partial Result Integration**: Incorporate completed work into retry attempts
  - **Incremental Recovery**: Continue building on existing progress rather than discarding it
  - **Technical Implementation**:
    - Store task execution state in `TaskExecutionState` with checkpoints
    - Implement `IExecutionStateManager` for state persistence and recovery
    - Add `ResumeFromCheckpoint()` method to orchestrator
    - Create `ExecutionCheckpoint` model with agent state, partial results, and failure context
    - Implement state serialization/deserialization for in-memory and future database storage
    - Add retry strategies: `ResumeFromLastSuccess`, `ResumeFromFailure`, `SkipFailedSubtasks`
- [ ] **Continue from Shutdown**: Manual continue action for tasks interrupted by system shutdown
  - **Technical Implementation**:
    - Add `ShutdownRecoveryService` to detect and catalog interrupted tasks
    - Implement `GetInterruptedTasks()` endpoint for UI display
    - Add `ContinueInterruptedTask(Guid taskId)` API endpoint
    - Store shutdown state in `TaskShutdownState` with timestamp and last known status
- [ ] **Continue Button**: Add continue functionality alongside retry for stuck pending tasks
  - **Technical Implementation**:
    - Add `Continue` action to `TaskAction` enum alongside `Retry`, `ForceExecute`
    - Implement `ContinueTask(Guid taskId)` in `ResearchOrchestrator`
    - Add continue button to `TaskCard.svelte` and `TaskDetails.svelte`
    - Logic: Resume from last successful checkpoint, skip failed subtasks if possible
- [ ] **Task Approval Workflow**: New kanban column for tasks requiring manual approval
  - **Overflow Task Management**: Handle tasks exceeding max depth as new root tasks with sibling connections
  - **Approval Queue**: Separate lane for tasks waiting for human approval before execution
  - **Technical Implementation**:
    - Add `RequiresApproval` status to `TaskStatus` enum
    - Create `ApprovalRequest` model with requester, reason, and approval criteria
    - Implement `RequestApproval(Guid taskId, string reason)` API endpoint
    - Add approval workflow to `TaskAnalyzerAgent` when depth > max depth
    - Create `ApprovalColumn.svelte` component for the new kanban lane
    - Add approval actions: `Approve`, `Reject`, `RequestChanges` with comments

#### Finalization Pipeline & Publication
- [ ] **Auto-Trigger Finalization Pipeline (Root Completion)**
  - **Technical Implementation**:
    - Add orchestrator hook `OnRootTaskCompleted(Guid rootTaskId)` that enqueues finalization subtasks with explicit dependencies: Outline → SectionDraft → FactCheck → CitationNormalize
    - Mark finalization subtasks as `IsSystemTask = true` and `TaskCategory = Finalization` for UI filtering
    - Ensure idempotency: detect and skip if a step already completed for this root; safe on retries/restarts
    - Emit fine-grained SSE events: `FinalizationStarted`, `FinalizationProgress`, `FinalizationCompleted`, `FinalizationFailed`
    - Persist outputs in `TaskReportEntity` (outline structure, section contents, normalized citations, status, version)
    - Use Semantic Kernel native functions/skills for agent calls and structured outputs
  - **Acceptance**:
    - Completing a root task automatically schedules and executes the four finalization steps in order
    - Finalization progress is visible on the board and via a per-task activity stream
    - Finalization is resumable and idempotent after restarts
- [ ] **In-App Report Publication (Viewer + Download)**
  - **Technical Implementation**:
    - Backend endpoints: `GET /api/reports/{taskId}` returns Markdown + metadata; `GET /api/reports/{taskId}/download?format=md`
    - Frontend (SvelteKit): add `/tasks/[id]/report` page that renders Markdown and shows citation backlinks; Tailwind layout
    - Show loading/finalizing state with auto-refresh when `FinalizationCompleted` arrives via SSE
    - Add “Download Markdown” action (PDF export delivered later alongside existing Export Capabilities)
  - **Acceptance**:
    - User can open a completed task and read a formatted report in-app
    - User can download the Markdown; PDF slated as a follow-up

## PR Plan: Finalization & Publication

- [ ] PR 1 — Orchestrator finalization trigger and domain scaffolding
  - **Scope**: Auto-trigger finalization when a root task completes; create system subtasks (task cards) for Outline → SectionDraft → FactCheck → CitationNormalize; add SSE events; minimal persistence for report status
  - **Backend**:
    - Add orchestrator hook `OnRootTaskCompleted(rootTaskId)` that enqueues finalization subtasks with explicit dependencies and `IsSystemTask = true`, `TaskCategory = Finalization`
    - Emit SSE events: `FinalizationStarted`, `FinalizationProgress`, `FinalizationCompleted`, `FinalizationFailed`
    - Persist `TaskReportEntity` with status, version, timestamps; map in `AppDbContext`
    - Ensure idempotency and safe retries
  - **Files (indicative)**: orchestrator, `TaskStatus/TaskCategory` enum, `TaskReportEntity`, `AppDbContext`, SSE DTOs/endpoint wiring
  - **Tests**: enqueue order, idempotency, SSE emission
  - **Docs**: create `docs/Finalization-Pipeline.md` (architecture, data flow, execution flow, how to test)
  - **Commit**: `feat(orchestrator): auto-trigger finalization pipeline on root completion`

- [ ] PR 2 — UI task cards for finalization subtasks (SvelteKit + Tailwind)
  - **Scope**: Render finalization subtasks as visible system task cards with badges; show live progress via SSE
  - **Frontend**:
    - Add `Finalization` category visuals and “system” badge
    - Display dependency order and step status (queued/running/done/failed) with loading states
  - **Files (indicative)**: `KanbanBoard.svelte`, `TaskCard.svelte`, `TaskDetails.svelte`, SSE store
  - **Tests**: component rendering, SSE event handling
  - **Docs**: add UI notes to `docs/Finalization-Pipeline.md` (UX, test steps)
  - **Commit**: `feat(ui): show finalization system subtasks with live progress`

- [ ] PR 3 — Wire finalization agents and retries (Semantic Kernel–aligned)
  - **Scope**: Execute `ReportOutlineAgent`, `SectionWriterAgent`, `QualityAssessmentAgent`, `CitationManagerAgent` in order with retries/backoff; structured outputs
  - **Backend**:
    - Implement step handlers writing artifacts to `TaskReportEntity` (outline, drafts, normalized citations)
    - Bounded exponential backoff; propagate actionable failure messages
  - **Files (indicative)**: agent invocation layer, SK function registrations, finalization service, repository updates
  - **Tests**: happy path + retry, structured output contracts
  - **Docs**: sequence diagram and test instructions in `docs/Finalization-Pipeline.md`
  - **Commit**: `feat(agents): wire finalization step handlers with retries and structured outputs`

- [ ] PR 4 — Report API endpoints and repository
  - **Scope**: Expose final report and metadata; Markdown download (PDF later)
  - **Backend**:
    - `GET /api/reports/{taskId}` returns Markdown + metadata
    - `GET /api/reports/{taskId}/download?format=md` for file download
    - Add `ITaskReportRepository` implementation; 404 and error shapes
  - **Files (indicative)**: `ReportsController`, repository interface/impl, `AppDbContext` mapping
  - **Tests**: controller (200/404), repository (insert/read), response schema
  - **Docs**: API usage in `docs/Finalization-Pipeline.md` and `docs/API-Reports.md`
  - **Commit**: `feat(api): add report read and markdown download endpoints`

- [ ] PR 5 — In-app report viewer and download
  - **Scope**: `/tasks/[id]/report` route; Markdown render; auto-refresh on finalization; download button
  - **Frontend**:
    - Show “Finalizing…” until `FinalizationCompleted` then render report and citations
    - “Download Markdown” action using the new endpoint
  - **Files (indicative)**: `routes/tasks/[id]/report/+page.svelte`, Markdown renderer, SSE subscription store
  - **Tests**: loading → ready transition, markdown render snapshot, download link presence
  - **Docs**: UI usage and end-to-end test steps in `docs/Finalization-Pipeline.md`
  - **Commit**: `feat(ui): add in-app report viewer and markdown download`

### Agent System Enhancements

#### Dynamic System Prompts
- [ ] **Domain-Aware Agent Prompts**: Generate specialized prompts based on task content
  - **Medical Expert Prompts**: Specialized prompts for medical research tasks
  - **Legal Research Prompts**: Expert prompts for legal analysis tasks
  - **Technical Implementation Prompts**: Specialized prompts for technical tasks
  - **Creative Research Prompts**: Prompts optimized for creative and exploratory tasks
  - **Technical Implementation**:
    - Create `IPromptGenerator` interface with `GeneratePrompt(Task task, AgentType agentType)` method
    - Implement `DomainAwarePromptGenerator` using LLM for domain classification
    - Add `PromptTemplate` model with placeholders for domain-specific content
    - Create prompt templates for each domain: `MedicalTemplate`, `LegalTemplate`, `TechnicalTemplate`, `CreativeTemplate`
    - Implement `PromptContext` with task description, domain, complexity, and agent role
    - Add prompt caching in `PromptCache` to avoid regeneration for similar tasks
- [ ] **Prompt Generation Strategies**:
  - **Keyword Analysis**: Detect domain-specific terminology
  - **Semantic Classification**: Use embeddings for task domain classification
  - **Context Inference**: Analyze task description and metadata for prompt optimization
  - **Expert Role Assignment**: Assign appropriate expert personas based on task type
  - **Technical Implementation**:
    - Create `IDomainClassifier` interface with `ClassifyDomain(TaskDescription)` method
    - Implement `KeywordBasedClassifier` for fast domain detection
    - Add `EmbeddingBasedClassifier` for semantic domain classification
    - Create `ExpertPersona` model with role, expertise, and prompt style
    - Implement `PersonaSelector` that chooses appropriate expert based on domain and complexity

### User Interface & Experience

#### Kanban Board Enhancements
- [ ] **Stacked Task Cards**: Visual stack illusion for completed parent-subtask groups
  - **Completed Column Stacking**: Show parent with completed subtasks as a single stacked card
  - **Visual Stack Effect**: Subtle offset backgrounds and (+N) indicators for hidden subtasks
  - **Stack Behavior**: Only apply to completed tasks; other columns show individual cards
  - **Technical Implementation**:
    - Add `_stackCount` property to `TaskItem` interface for UI-only stacking data
    - Implement `TaskStackingService` to calculate which tasks should be stacked
    - Create `calculateStackedTasks(tasks: TaskItem[])` method in `KanbanBoard.svelte`
    - Add CSS classes for stacked appearance: `.task-stack`, `.task-stack-background`, `.stack-count-badge`
    - Implement stacking logic: parent + all completed children = single stacked card
    - Add stack hover effects to show hidden subtask details
- [ ] **Approval Lane**: New kanban column for tasks awaiting manual approval
  - **Technical Implementation**:
    - Add `ApprovalColumn.svelte` component with approval actions
    - Create `ApprovalTaskCard.svelte` with approve/reject/revision buttons
    - Add approval state management in `approvalStore.ts`
    - Implement approval workflow: `RequestApproval`, `Approve`, `Reject`, `RequestChanges`
- [ ] **Task Relationship Visualization**: Better display of parent-child and sibling relationships
  - **Technical Implementation**:
    - Add relationship indicators to `TaskCard.svelte`: parent/child/sibling icons
    - Create `TaskRelationshipVisualizer` component for complex relationship trees
    - Implement relationship data in `TaskItem` interface: `parentId`, `childIds`, `siblingIds`
    - Add visual connectors between related tasks in the kanban board

#### Data Interaction & Chat
- [ ] **Chat Interface for Stored Data**: Interactive chat with Qdrant and SQL data
  - **Research Data Chat**: Ask questions about stored research results
  - **Vector Database Query**: Natural language queries for semantic memory
  - **SQL Data Exploration**: Chat-based exploration of task and result data
  - **Context-Aware Responses**: Chat responses that understand task relationships and history
  - **Technical Implementation**:
    - Create `ChatInterface.svelte` component with chat input and message history
    - Implement `IChatService` interface with `SendMessage(string message)` method
    - Create `ResearchDataChatService` that queries Qdrant and SQL data
    - Add `ChatMessage` model with user input, system response, and data sources
    - Implement `QueryParser` to convert natural language to structured queries
    - Add `ResponseGenerator` that formats data into natural language responses
    - Create chat endpoints: `POST /api/chat`, `GET /api/chat/history`
    - Implement chat state management in `chatStore.ts` with message history and context

### Data Storage & Persistence

#### Database Migration
- [x] **SQL Server Migration**: Move from current storage to actual MSSQL
  - **Entity Framework Implementation**: Proper ORM for data persistence
  - **Data Migration Scripts**: Migrate existing in-memory data to SQL Server
  - **Connection String Management**: Secure database connection configuration
  - **Technical Implementation**:
    - Create `AppDbContext` with Entity Framework Core for all domain models
    - Implement `ITaskRepository`, `IEventRepository` interfaces (results repository pending)
    - Add `TaskEntity`, `TaskEventEntity`, `TaskReportEntity` with proper relationships
    - Create `DatabaseMigrationService` to handle in-memory to SQL migration
    - Implement `IDataPersistenceService` with `SaveTask`, `LoadTask`,