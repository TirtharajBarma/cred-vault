# CASE STUDY: CredVault — Architecting a Resilient Distributed System for Modern Financial Operations

## Executive Summary
**CredVault** is an enterprise-grade Credit Card Management and Bill Payment platform engineered to deliver high-performance financial services with uncompromising data integrity. By leveraging a **Microservices Architecture** built on **.NET 8**, **Angular**, and **RabbitMQ**, CredVault addresses the critical challenges of distributed systems: consistency, availability, and security. The cornerstone of its reliability is a sophisticated **Saga Orchestration** pattern that ensures atomic transactions across disparate databases, providing a seamless and secure experience for complex financial workflows.

---

## 1. The Narrative: From Monolith Constraints to Microservice Agility
In the fast-evolving financial sector, traditional monolithic architectures often become "anchors" rather than "engines." As systems grow, they suffer from:
*   **Tight Coupling:** A minor change in the notification logic could inadvertently crash the payment processor.
*   **Scaling Inefficiency:** The entire application must be scaled even if only the "Statement Generation" module is under heavy load.
*   **Deployment Friction:** Large codebases lead to longer CI/CD cycles and higher operational risk.

**CredVault** was born from a "Microservices-First" philosophy. By decomposing the domain into autonomous Bounded Contexts—**Identity, Cards, Billing, and Payments**—we achieved a system where each service can evolve, scale, and fail independently without compromising the global state.

---

## 2. Technical Strategy: Why This Stack?

The CredVault ecosystem is built on a foundation of industry-leading technologies, chosen for their synergy in high-stakes environments:

*   **Frontend (Angular):** Provides a robust, type-safe framework for building complex, reactive dashboards and secure payment interfaces.
*   **Backend (.NET 8):** Chosen for its high-performance Kestrel web server, mature dependency injection, and first-class support for cloud-native patterns.
*   **API Gateway (Ocelot):** Acts as a defensive perimeter and unified entry point, abstracting internal service topology and centralizing cross-cutting concerns like authentication and rate limiting.
*   **Messaging (RabbitMQ & MassTransit):** The backbone of our asynchronous communication. **MassTransit** simplifies the implementation of the Saga pattern, handling the complexities of state management and message retries.
*   **Persistence (SQL Server per Service):** Ensures strict data ownership. Each service manages its own schema, preventing the "Shared Database Anti-pattern" and ensuring that a schema change in one service never breaks another.

---

## 3. Engineering Challenges: Mastering Distributed Consistency

### The Dual-Write Problem
One of the greatest challenges in microservices is the "dual-write" problem: updating a database and publishing an event simultaneously. If one fails, the system becomes inconsistent.

### Distributed Data Consistency via the Saga Pattern
CredVault solves this using the **Orchestrated Saga Pattern**. When a user pays a bill, the process involves multiple services (Payment, Billing, Card). Instead of a traditional distributed transaction (2PC) which is slow and prone to blocking, we use a **State Machine** to coordinate:
1.  **Initiation:** The Payment Service creates a pending record.
2.  **Execution:** Commands are sent to the Billing Service (to update dues) and the Card Service (to deduct balances).
3.  **Compensating Actions:** If the Card Service fails (e.g., insufficient funds), the Saga automatically triggers a "Compensating Transaction" in the Billing Service to roll back the previous update, ensuring **Eventual Consistency**.

---

## 4. Implementation Detail: The Secure 2-Step Payment Process

To balance user experience with high security, CredVault implements a rigorous payment flow:

1.  **Step 1: Initiation & Challenge:** The user initiates a payment. The system validates ownership of the bill and generates a secure, hashed **OTP (One-Time Password)** stored in the Payment context.
2.  **Step 2: Verification & Saga Trigger:** Upon submitting the correct OTP, the **Saga State Machine** is activated. It orchestrates the distributed workflow across RabbitMQ, tracking every state transition (AwaitingOTP -> Processing -> Completed/Failed) in a persistent log to ensure recovery from any infrastructure failure.

---

## 5. Security: Defensive Depth
Financial data requires a multi-layered security approach:

*   **API Gateway Isolation:** Internal microservices are hidden within a private virtual network. The only ingress is through the **Ocelot Gateway**, which validates **JWT (JSON Web Tokens)**.
*   **Data Masking & Encryption:** Sensitive card numbers are never stored in plain text. They are protected using **AES-256 encryption** at the application layer and masked (`**** 1234`) for UI display.
*   **Token-Based Authorization:** Identity is managed centrally, but authorization is enforced at the service level, ensuring that even if one service is compromised, the attacker cannot traverse the entire system.

---

## 6. Impact: A Decoupled Future
The transition to this architecture has yielded significant dividends:
*   **Fault Tolerance:** A failure in the Notification Service no longer prevents users from paying their bills.
*   **Independent Scalability:** We can scale the Payment Service 5x during peak bill-pay cycles without affecting the Identity Service.
*   **Developer Velocity:** Teams can deploy updates to the "Rewards" module in the Billing Service without coordinating a full-system release.

**CredVault** stands as a testament to the power of modern architectural patterns in solving the age-old problems of financial software: reliability, security, and scale.
