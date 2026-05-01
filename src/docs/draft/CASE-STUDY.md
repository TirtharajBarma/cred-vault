# Case Study — CredVault

## Building a Resilient Credit Card Management Platform  
#### Microservices + Saga Orchestration

---

## 1. Executive Summary

CredVault is a credit card management platform that handles the full card lifecycle, from issuance and billing to rewards and secure payments.

The system was designed using a microservices architecture to overcome common limitations of monolithic financial systems, including:
- Tight coupling  
- Scaling bottlenecks  
- Deployment risk  

The core technical challenge was ensuring reliable data consistency across distributed services without relying on traditional two-phase commit (2PC) transactions.

This was addressed using the Saga pattern with MassTransit, enabling:
- Reliable distributed payments  
- Automatic compensation on failure  
- Improved fault tolerance across services  

---
## 2. Problem Statement
Traditional credit card management systems built as monoliths face several problems:
| Problem | Impact |
|---------|--------|
| **Tight coupling** | A bug in notification logic can take down payment processing |
| **Scaling inefficiency** | The entire system must scale even if only one module is under load |
| **Deployment risk** | Large codebases lead to slow CI/CD pipelines and high rollback costs |
| **Data inconsistency** | Cross-service operations (payment → billing → card deduction) fail partially, leaving orphaned records |
| **Security surface** | A single compromised module exposes the entire system |
CredVault was built to solve these problems through domain-driven decomposition and event-driven communication.
---
## 3. Architecture Decisions
### 3.1 Why Microservices?
The platform was decomposed into five bounded contexts based on business capabilities:
| Service | Bounded Context | Rationale |
|---------|----------------|-----------|
| Identity | User authentication & authorization | Independent security domain |
| Card | Card lifecycle & transactions | Card data has different access patterns |
| Billing | Bills, statements, rewards | Billing logic is computationally heavy |
| Payment | Payment flow & wallet | Requires highest availability and consistency |
| Notification | Email delivery & audit logs | Pure side-effect service, can fail without blocking |
Each service owns its database — no shared tables, no cross-service foreign keys.
### 3.2 Technology Selection
| Decision | Choice | Reason |
|----------|--------|--------|
| Backend framework | .NET 8 / ASP.NET Core | High performance, mature DI, excellent async support |
| Frontend | Angular 21.2 | Type-safe, component-based, enterprise-ready |
| API Gateway | Ocelot | Lightweight, URL-based routing, .NET native |
| Message Broker | RabbitMQ + MassTransit | Reliable delivery, built-in retry, saga support |
| Database | SQL Server 2022 | ACID guarantees per service, EF Core integration |
| Auth | JWT Bearer + Google OAuth | Stateless, scalable, industry standard |
### 3.3 Why Not 2PC (Two-Phase Commit)?
Traditional distributed transactions (2PC) were rejected because:
- **Blocking**: All services must be online simultaneously
- **Performance**: Locks held across services during the prepare phase
- **Scalability**: Doesn't work well with high-throughput systems
- **Complexity**: Difficult to debug and recover from partial failures
Instead, CredVault uses **Saga orchestration** with compensation — services complete their work independently, and failures are handled through rollback events.
---
## 4. Key Technical Challenges
### 4.1 Challenge: Distributed Payment Consistency
**Problem:** A bill payment involves three services — Payment (initiates), Billing (updates bill), and Card (deducts balance). If any step fails after a previous step succeeds, the system is left in an inconsistent state.
**Solution:** Implemented a **choreography-based Saga** using MassTransit State Machine:
```
Payment Initiated
  → Update Bill (Billing Service)
  → Redeem Rewards (Billing Service) — optional
  → Deduct Card Balance (Card Service)
  → Payment Completed
On any failure:
  → Reverse Rewards (if redeemed)
  → Revert Bill (if updated)
  → Refund Wallet (if debited)
  → Payment Compensated
```
**Reliability mechanisms:**
- **InMemoryOutbox**: Prevents lost messages during service restarts
- **Retry policy**: Exponential backoff (1s → 5s → 15s) on all consumers
- **State persistence**: Saga state stored in database; survives crashes
- **Idempotency**: CorrelationId ensures duplicate messages are ignored
### 4.2 Challenge: Secure Card Data Storage
**Problem:** Credit card numbers are highly sensitive and must be protected against database breaches.
**Solution:** Three-layer protection:
1. **Encryption at rest**: Card numbers encrypted with AES before storage
2. **Masked display**: Only last 4 digits stored in plain text for UI display
3. **No CVV storage**: CVV is never stored — only used for initial validation
### 4.3 Challenge: OTP Delivery Reliability
**Problem:** OTP emails must reach users within seconds. If the email service is down, payments and registrations should not block.
**Solution:** **Asynchronous notification via RabbitMQ**:
- Services publish events and continue immediately
- Notification Service consumes events independently
- If Notification Service is down, messages queue in RabbitMQ
- Failed email sends are retried with exponential backoff
- All attempts are logged in `NotificationLogs` for audit
### 4.4 Challenge: Preventing Duplicate Payments
**Problem:** A user clicking "Pay" twice could trigger two payment sagas for the same bill.
**Solution:** Multiple safeguards:
- Stuck payment cleanup before new initiation
- Bill status validation (only Pending/PartiallyPaid can be paid)
- Saga idempotency via CorrelationId
- OTP expiry (5 minutes) prevents stale payment execution
---
## 5. Architecture Patterns in Practice
| Pattern | Where Applied | Benefit |
|---------|---------------|---------|
| **API Gateway** | Ocelot routes all client requests | Services hidden from public network |
| **CQRS** | MediatR separates commands from queries | Independent scaling of reads vs writes |
| **Clean Architecture** | Domain → Application → Infrastructure → API | Testable, maintainable, dependency-free domain |
| **Saga Pattern** | Payment orchestration across 3 services | Eventual consistency without 2PC |
| **Outbox Pattern** | MassTransit `UseInMemoryOutbox()` | No lost messages during crashes |
| **Event-Driven** | RabbitMQ for all inter-service communication | Temporal decoupling, fault tolerance |
| **Soft Deletes** | Card service `IsDeleted` flag | Audit trail preserved, reversible |
| **Shared Contracts** | Common DTOs, events, middleware library | Consistency across all services |
---
## 6. Security Architecture
### Defense in Depth
| Layer | Mechanism |
|-------|-----------|
| **Network** | Only Gateway (port 5006) exposed; services on private Docker network |
| **Authentication** | JWT Bearer tokens, validated by every service |
| **Authorization** | RBAC at service level — `[Authorize(Roles = "admin")]` |
| **Data** | AES encryption for card numbers, BCrypt for passwords |
| **Transport** | HTTPS for all external communication |
| **Application** | FluentValidation on all inputs, global exception middleware |
| **Audit** | All admin actions logged with JSON change diffs |
---
## 7. Results
| Metric | Outcome |
|--------|---------|
| **Fault isolation** | Notification Service failure does not block payments or login |
| **Independent scaling** | Payment Service can scale 5x during peak without affecting Identity |
| **Deployment speed** | Each service deploys independently; no coordinated releases needed |
| **Data consistency** | Saga compensation ensures no orphaned payments or unreversed bills |
| **Developer experience** | Clean Architecture enables focused testing per layer |
---
## 8. Lessons Learned
1. **Saga complexity is real**: Designing compensation paths requires careful thinking about every possible failure point. It's better to over-compensate than under-compensate.
2. **Event contracts are contracts**: Once an event is published, changing its schema breaks consumers. Version events early and treat them as public APIs.
3. **Distributed logging is essential**: Without correlation IDs flowing through every request and event, debugging saga failures becomes nearly impossible.
4. **Database-per-service is non-negotiable**: Shared databases create hidden coupling that undermines the entire microservices approach.
5. **Test the failure paths**: The happy path is easy. The real value of Saga is in compensation — test every failure scenario thoroughly.
---
## 9. Future Considerations
| Area | Potential Improvement |
|------|----------------------|
| **Observability** | Add OpenTelemetry for distributed tracing across services |
| **Caching** | Redis for frequently-read data (reward tiers, issuer list) |
| **Message persistence** | Switch from InMemoryOutbox to EF Core Outbox for crash recovery |
| **API versioning** | Implement `/api/v2/` for breaking changes |
| **Rate limiting** | Distributed rate limiting via Redis (currently in-memory) |
| **CI/CD** | GitHub Actions with per-service pipelines |
| **Load testing** | k6 scenarios for payment flow under concurrent load |
---
*End of Case Study*