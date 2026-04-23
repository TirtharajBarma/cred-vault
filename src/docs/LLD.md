# Enterprise Low-Level Design (LLD) Specification

**System Name:** CredVault Credit Card Management Platform  
**Target Audience:** Core Engineering, Architecture, DBA Teams, Quality Assurance (QA), and Integration Teams  
**Document Version:** 5.0 (Enterprise Standard Specification)  
**Date:** 2026-04-14  

> [!NOTE] 
> This document remains the authoritative engineering contract for the CredVault ecosystem. It prescribes software patterns, bounded contexts (via Docker containers), physical database schemas, distributed event messaging, complex Saga State Machine orchestration, and unified API paradigms.

---

## 1. Document Control

| Version | Date | Author | Description of Changes |
|---------|------|--------|------------------------|
| 4.1     | 2026-04-13 | Architecture Team | Microservice splitting and MassTransit integration. |
| 5.0     | 2026-04-14 | Lead Architect | Enterprise standardization, enhanced diagram legibility, rich component contexts, and robust database documentation. |

---

## Table of Contents
1. [Executive Summary & Design Principles](#1-executive-summary--design-principles)
2. [Component Architecture Diagram](#2-component-architecture-diagram)
3. [Enterprise Database Architecture (Detailed DB Schemas)](#3-enterprise-database-architecture-detailed-db-schemas)
4. [Event Broker Architecture (RabbitMQ)](#4-event-broker-architecture-rabbitmq)
5. [Distributed Workflows & Saga Pattern Sequences](#5-distributed-workflows--saga-pattern-sequences)
6. [Core Business Logic Flow Instructions (Pseudocode)](#6-core-business-logic-flow-instructions-pseudocode)
7. [Deployment Architecture](#7-deployment-architecture)

---

## 1. Executive Summary & Design Principles

CredVault enforces highly decoupled architecture mapped strictly to independent sub-domains. It leverages **Command Query Responsibility Segregation (CQRS)**, **Clean Layered Architecture**, and asynchronous **Event-Driven Messaging** for distributed data consistency.

### 1.1 Structural Implementation Patterns
| Pattern Achieved | Implementation Mechanism | Strategic Trade-off / Benefit |
|---|---|---|
| **Clean Architecture** | Rigid horizontal project separation: `Domain` (entities), `Application` (use cases), `Infrastructure` (data), `Presentation` (API). | Business logic is completely decoupled from ASP.NET specifics or EF Core ORMs. |
| **CQRS Pattern** | Leveraging `MediatR` to isolate `CommandHandlers` (mutating Writes) from `QueryHandlers` (read-only queries). | Performance tuning can be scaled asymmetrically; enables future database read-replicas. |
| **Saga State Machine** | Orchestrated by `MassTransit` over `RabbitMQ`, functioning as a central coordinator for complex distributed transactions. | Solves the dual-write cross-database problem, providing out-of-box compensation (rollback) logic. |
| **API Gateway Routing** | Reverses proxying via `Ocelot`, defining static upstream URLs against dynamic downstream container internal IPs. | Shields client UI (Angular) from internal port collisions and service topology changes. |

---

## 2. Component Architecture Diagram

The Component Model demonstrates how functional sub-domains interface via the Shared Gateway and Event Bus.

```mermaid
flowchart LR
    %% External Nodes
    UI([📱 Angular Client UI])
    GW{🚪 Ocelot API Gateway}

    %% Core Services Boundary
    subgraph Microservices_Layer [Microservices Layer]
        ID[Identity Service Controller]
        CD[Card Service Controller]
        BL[Billing Service Controller]
        PM[Payment Service Controller]
        NT[Notification Engine Worker]
    end

    %% Abstractions
    subgraph Contracts [Domain Abstraction]
        SH[[Shared.Contracts Library]]
    end

    %% Infrastructure
    subgraph Infra [State & Message Infrastructure]
        MQ[[RabbitMQ Event Bus]]
        SQL[(Distributed SQL Server)]
    end

    %% Gateway Routing
    UI ==>|REST HTTPS: 5006| GW
    GW -->|/api/identity/*| ID
    GW -->|/api/cards/*| CD
    GW -->|/api/billing/*| BL
    GW -->|/api/payments/*| PM

    %% Contract Coupling
    ID -.->|depends on| SH
    CD -.->|depends on| SH
    BL -.->|depends on| SH
    PM -.->|depends on| SH
    NT -.->|depends on| SH

    %% Data Connections
    ID -->|EF Core DbContext| SQL
    CD -->|EF Core DbContext| SQL
    BL -->|EF Core DbContext| SQL
    PM -->|EF Core DbContext| SQL
    NT -->|Dapper Read-Only| SQL

    %% Bus Connections
    ID --->|Publishes Events| MQ
    CD <-->|Pub/Sub Constraints| MQ
    BL <-->|Pub/Sub Constraints| MQ
    PM --->|Orchestrates Sagas| MQ
    NT <---|Consumes Email Triggers| MQ

    %% Styling
    classDef Client fill:#8e44ad,stroke:#fff,stroke-width:2px,color:#fff;
    classDef Gate fill:#e67e22,stroke:#fff,stroke-width:2px,color:#fff;
    classDef Service fill:#2980b9,stroke:#fff,stroke-width:2px,color:#fff;
    class UI Client
    class GW Gate
    class ID,CD,BL,PM,NT Service
```

*Figure 2.1: Macro Component Diagram. The UI interacts solely with the generic `Ocelot` endpoint. Independent microservices communicate asynchronously via RabbitMQ using standardized structures contained deeply within `Shared.Contracts`.*

---

## 3. Enterprise Database Architecture (Detailed DB Schemas)

Data ownership is extremely strict. Every bounded context relies on an isolated schema. **Cross-database SQL Joins are fundamentally illegal in this architecture.** Primary keys (GUIDs) act as logical foreign keys across service boundaries.

### 3.1 Card Bounded Context (`credvault_cards`)
This context strictly manages the user's financial instruments, decoupling card details from identity or payments.

```mermaid
erDiagram
    CardIssuers {
        uniqueidentifier Id PK
        string Name "e.g. HDFC, Chase"
        int Network "Enum: Visa/MasterCard"
    }

    CreditCards {
        uniqueidentifier Id PK
        uniqueidentifier UserId "Logical FK to Identity"
        uniqueidentifier IssuerId FK
        string CardholderName "Exact matching name"
        string MaskedNumber "**** 1234"
        string EncryptedCardNumber "AES-256 string"
        decimal CreditLimit
        decimal OutstandingBalance
        datetime CreatedAtUtc
    }

    CardTransactions {
        uniqueidentifier Id PK
        uniqueidentifier CardId FK
        int Type "Enum: Charge / Payment"
        decimal Amount
        datetime DateUtc
    }

    CardIssuers ||--o{ CreditCards : "issues"
    CreditCards ||--o{ CardTransactions : "contains"
```
*Figure 3.1: Card Schema. Evaluates strict limits based on the outstanding vs credit limit boundaries. Encrypted fields are exclusively handled in transit.*

### 3.2 Billing Bounded Context (`credvault_billing`)
Calculates cyclic interest, outstanding dues, and aggregates customer reward properties.

```mermaid
erDiagram
    Bills {
        uniqueidentifier Id PK
        uniqueidentifier CardId "Logical FK to Card"
        decimal Amount "Total outstanding"
        decimal MinDue
        datetime DueDateUtc
        int Status "Enum: Partial, Paid, Owed"
    }

    RewardAccounts {
        uniqueidentifier Id PK
        uniqueidentifier UserId "Logical FK"
        decimal PointsBalance
        datetime UpdatedAtUtc
    }

    RewardTransactions {
        uniqueidentifier Id PK
        uniqueidentifier RewardAccountId FK
        decimal Points
        int Type "Enum: Credited, Redeemed"
    }

    Statements {
        uniqueidentifier Id PK
        uniqueidentifier BillId FK
        string StatementPeriod
        decimal ClosingBalance
    }

    RewardAccounts ||--o{ RewardTransactions : "tracks"
    Bills ||--o{ RewardTransactions : "sources"
    Bills ||--o{ Statements : "mapped to"
```
*Figure 3.2: Billing Schema. `Bills` serves as the primary root aggregate containing generated Statements and calculating required minimums.*

### 3.3 Payments Isolation Context (`credvault_payments`)
The source of absolute truth for Top-Ups and distributed Bill Payment sagas.

```mermaid
erDiagram
    Payments {
        uniqueidentifier Id PK
        uniqueidentifier UserId
        uniqueidentifier BillId
        decimal Amount
        int Status "AwaitingOTP, Processed, Failed"
        string OtpCode "Hashed Verification"
    }

    PaymentOrchestrationSagas {
        uniqueidentifier CorrelationId PK "Saga Tracking GUID"
        uniqueidentifier PaymentId "Logical Context"
        string CurrentState "State Machine Node"
        bool OtpVerified "Flags"
        bool CardDeducted "Flags"
        int CompensationAttempts "Retry counts"
    }

    Payments ||--o| PaymentOrchestrationSagas : "orchestrated by"
```
*Figure 3.3: Payment Schema. Notice the existence of the physical Saga state log (`PaymentOrchestrationSagas`). This table functions as a persistent lock for long-running workflows.*

---

## 4. Event Broker Architecture (RabbitMQ)

Credvault adheres strictly to decoupling services over Rabbit MQ. Two distinct messaging paradigms exist:
1. **Domain Events (Pub/Sub):** Fire & Forget notifications (e.g. `UserRegistered`, `StatementGenerated`). The Publisher does not care who consumes it.
2. **Distributed Requests (Saga Orchestration):** Managed exclusively by the Payment Saga Orchestrator handling deterministic point-to-point calls over RabbitMQ.

```mermaid
flowchart TB
    %% Core Engines
    subgraph Publisher_Services [Services Emitting Events]
        IS[Identity Engine]
        CS[Card Engine]
        PS[Payment Orchestrator]
    end

    %% Message Bus Server
    subgraph Broker [RabbitMQ Server Topology]
        direction TB
        
        %% Topic Exchanges
        subgraph Exchanges Layer
            E_ID(identity-topic-exchange)
            E_PAY(payment-saga-exchange)
        end
        
        %% Queue Binding
        subgraph Target Queues
            Q_NOTIF[notif-email-queue]
            Q_SAGA[payment-orchestration-queue]
            Q_PROC[card-deduct-queue]
        end

        E_ID --->|Routing Key: user.*| Q_NOTIF
        E_PAY --->|Routing Key: saga.command.*| Q_SAGA & Q_PROC
    end

    %% Emitting Connections
    IS --> E_ID
    PS --> E_PAY
    CS --> E_PAY

    %% Consumption Workers
    Q_NOTIF -->|Async Consumes| NS[Notification SMTP Workers]
    Q_SAGA -->|Async Consumes| SG[Saga Process Workers]
    Q_PROC -->|Async Validates| CA[Card Deduction Executers]
```
*Figure 4.1: Message Broker Map. Displays how topic-based exchanges fan out messages to strictly bounded target queues, feeding worker microservices.*

---

## 5. Distributed Workflows & Saga Pattern Sequences

The Saga State Machine orchestrates multiple separate database changes into **One Atomic Pseudo-Transaction**. Failure at any step triggers immediate compensation backwards.

### 5.1 Strict Execution Happy-Path (Orchestrated)
```mermaid
sequenceDiagram
    autonumber
    participant UI as Angular Frontend
    participant PA as Payment Orchestrator (Saga)
    participant BI as Billing Service
    participant CA as Card Service

    UI->>PA: Initiate Payment (BillId, Ext. Wallet)
    activate PA
    PA->>PA: Transition Saga to "Generating OTP"
    PA-->>UI: 2FA Required (Returns PaymentId)
    deactivate PA

    UI->>PA: Submit OTP (PaymentId, "1248")
    activate PA
    PA->>PA: Validated. Transition to "Processing"
    
    Note over PA, CA: Distributed Execution begins over RabbitMQ

    PA->>BI: Command -> Update Bill Paid Amount
    BI-->>PA: RabbitMQ Ack -> `IBillUpdateSucceeded`
    
    PA->>CA: Command -> Deduct Wallet/Card Outstanding
    CA-->>PA: RabbitMQ Ack -> `ICardDeductionSucceeded`
    
    PA->>PA: Transition Saga to "Completed"
    PA-->>UI: Payment Finalized Success
    deactivate PA
```
*Figure 5.1: Happy Path Saga. Illustrates a clean top-down execution whereby the user's OTP passes and all downstream database contexts execute their updates natively.*

### 5.2 The Compensation Retry Block (Orchestrated Failover)
If downstream infrastructure rejects an update (e.g. Card system is offline), preceding steps must be inherently reversed.

```mermaid
sequenceDiagram
    autonumber
    participant PA as Payment Orchestrator (Saga)
    participant BI as Billing Service
    participant CA as Card Service

    PA->>CA: Command -> Deduct Wallet/Card
    activate CA
    CA--XPA: Error Payload -> `ICardDeductionFailed`
    deactivate CA

    Note over PA: Serious Failure. Transition to COMPENSATING state.

    PA->>BI: Command -> Revert Bill Updates (Sub Amount)
    activate BI
    BI-->>PA: RabbitMQ Ack -> `IRevertBillUpdateSucceeded`
    deactivate BI

    PA->>PA: Transition to "Failed". End Saga.
```
*Figure 5.2: Compensation Flow. The orchestrator explicitly acts as a cleanup system, deploying reverse-commands to undo everything that previously succeeded, thus maintaining global consistency.*

---

## 6. Core Business Logic Flow Instructions (Pseudocode)

### 6.1 Payment Validation Constraints
All payment commands must pass the following rigorous logical checks before a Saga is even allowed to initiate.

```text
FUNCTION ValidateInitiatePaymentRequest(UserContext, IncomingBillId, RequestedAmount):
    
    Define Bill Entity = DB.Bills.FirstOrDefault(id == IncomingBillId)
    
    // Assertion 1: Existence & Ownership
    IF Bill Entity IS NULL -> Return HTTP 404 (Not Found)
    IF Bill.UserId != UserContext.Id -> Return HTTP 403 (Forbidden Cross-Tenant Violation)

    // Assertion 2: Mathematical Dues Validations
    Define OutstandingDelta = MAX(0, Bill.Amount - Bill.AmountPaid)
    IF OutstandingDelta <= 0 -> Return HTTP 400 (Bill Already Cleared)
    IF RequestedAmount <= 0 -> Return HTTP 400 (Amount must be positive)
    IF RequestedAmount > OutstandingDelta -> Return HTTP 400 (Overpayment not permitted)

    // Final Setup -> Execute
    Set OTP = GenerateSecureRandom(6)
    Save Initial Payment Stub (AwaitingOTP, Insert OTP Hash)
    Return HTTP 200 (Challenge Token)
END FUNCTION
```

---

## 7. Deployment Architecture (Docker Infrastructure)

Containers execute exclusively within an internal bridged network (`credvault-network`). External traffic is entirely restricted minus the gateway's `:5006` proxy port.

```mermaid
flowchart TB
    %% External Internet Space
    subgraph WAN [Public External Zone]
        EXT_UI[📱 Web / Mobile Client]
    end

    %% Host Machine Context
    subgraph Host [Docker Deamon Host]
        
        %% Core Network
        subgraph Net [credvault-network Docker Bridge]
            GWC[🚀 Ocelot Gateway :5006]
            IDC[🛡️ Identity Container]
            CDC[💳 Card Container]
            BLC[🧾 Billing Container]
            PMC[💸 Payment Container]
            NTC[📨 Notification Container]
            
            MQC[🐇 RabbitMQ Mgt :15672]
            SQLC[🗄️ SQL Server Engine :1433]
        end

        %% Hosted Volume Mounts
        subgraph Volumes [Docker Persistent Volumes]
            VSQL[(project_sqlserver_data)]
            VMQ[(rabbitmq_data)]
        end
    end

    %% Network Entry
    EXT_UI -->|HTTPS TCP 5006| GWC

    %% Static Container Mapping
    GWC -->|identity-api:80| IDC
    GWC -->|card-api:80| CDC
    GWC -->|billing-api:80| BLC
    GWC -->|payment-api:80| PMC

    %% Infrastructure Dependencies
    IDC & CDC & BLC & PMC & NTC --> SQLC
    IDC & CDC & BLC & PMC & NTC --> MQC

    %% Disk Persistence mappings
    SQLC === VSQL
    MQC === VMQ
```
*Figure 7.1: Deployment Infrastructure Diagram. Demonstrates exactly how Docker Compose assembles the network graph, ensuring stateful servers save to local persistent volumes outliving container ephemerality.*

---

## 8. Detailed System Diagrams (Class, State, Activity, Service Flow)

To fully complete the enterprise architecture requirements, the following models detail class structures, lifecycles, and low-level flows.

### 8.1 Class Diagrams

**Identity Service Class Diagram**
```mermaid
classDiagram
    class IdentityController {
        +RegisterUser(RegisterDto)
        +Login(LoginDto)
        +VerifyOtp(OtpDto)
    }

    class UserService {
        +CreateUserAsync(User)
        +ValidateCredentialsAsync(email, password)
        +GenerateJwtAsync(User)
    }

    class UserRepository {
        +GetByEmailAsync(email)
        +AddAsync(User)
        +UpdateAsync(User)
    }

    class TokenService {
        +CreateToken(User)
        +ValidateToken(tokenString)
    }

    IdentityController --> UserService
    UserService --> UserRepository
    UserService --> TokenService
```
*Figure 8.1: Identity Service Class Diagram mapping internal controllers to domain managers.*

**Payment Service Class Diagram**
```mermaid
classDiagram
    class PaymentsController {
      +InitiatePayment(request)
      +VerifyOtp(request)
    }

    class InitiatePaymentCommandHandler {
      +Handle(command, cancellationToken)
      -MarkStuckPaymentsFailedAsync()
      -GenerateOtp()
    }

    class PaymentRepository {
      <<Interface>>
      +AddAsync(Payment)
      +GetByIdAsync(Guid)
      +UpdateAsync(Payment)
    }

    class PaymentOrchestrationSaga {
      +StateMachineTransitions()
      +Event<IStartPaymentOrchestration> StartEvent
    }

    PaymentsController --> InitiatePaymentCommandHandler
    InitiatePaymentCommandHandler --> PaymentRepository
    InitiatePaymentCommandHandler --> PaymentOrchestrationSaga
```
*Figure 8.2: Payment Service Class Diagram highlighting CQRS segregation and State Machine bindings.*

### 8.2 State Diagram (Payment Lifecycle)

```mermaid
stateDiagram-v2
    [*] --> AwaitingOtp
    AwaitingOtp --> OtpVerified: Verify OTP Success
    AwaitingOtp --> Expired: OTP Timeout
    AwaitingOtp --> Failed: Invalid OTP limit reached

    OtpVerified --> Processing
    Processing --> BillUpdated: Bill update success
    BillUpdated --> RewardsRedeemed: Rewards redemption success
    BillUpdated --> CardDeducted: No rewards path
    RewardsRedeemed --> CardDeducted

    CardDeducted --> Completed

    Processing --> Compensating: Any downstream failure
    Compensating --> Failed: Compensation complete

    Completed --> [*]
    Failed --> [*]
    Expired --> [*]
```
*Figure 8.3: State Diagram visualizing the progression of a distributed transation in a 2FA environment.*

### 8.3 Activity Diagram (Payment Process)

```mermaid
flowchart TD
    Start([Start]) --> A[User selects bill and payment type]
    A --> B[System validates bill ownership & amount]
    B --> C{Valid request?}
    C -- No --> X[Return validation error] --> End([End])
    C -- Yes --> D[Generate OTP and create payment record]
    D --> E[User submits OTP]
    E --> F{OTP valid?}
    F -- No --> Y[Reject verification] --> End
    F -- Yes --> G[Start Distributed Saga]
    G --> H[Update bill paid amount]
    H --> I{Rewards selected?}
    I -- Yes --> J[Redeem rewards points]
    I -- No --> K[Skip rewards]
    J --> L[Deduct wallet/card]
    K --> L
    L --> M{All steps successful?}
    M -- Yes --> N[Mark payment completed]
    M -- No --> O[Trigger compensation]
    N --> End
    O --> End
```
*Figure 8.4: Activity flowchart highlighting logical conditions triggering external compensation handlers.*

### 8.4 Service Flow Diagram (All Microservices)

```mermaid
flowchart LR
    UI[Client UI] --> GW[API Gateway]

    GW --> ID[Identity Service]
    GW --> CD[Card Service]
    GW --> BL[Billing Service]
    GW --> PM[Payment Service]

    PM -->|IStartPaymentOrchestration| MQ[(RabbitMQ)]
    MQ -->|IBillUpdateRequested| BL
    MQ -->|IRewardRedemptionRequested| BL
    MQ -->|ICardDeductionRequested| CD
    
    ID -->|UserRegistered| MQ
    BL -->|BillGenerated| MQ
    PM -->|PaymentCompleted| MQ
    
    MQ -->|SendEmail| NT[Notification Service]
```
*Figure 8.5: High-level Service Flow detailing the interaction boundaries mediated via Gateway and Broker topology.*
