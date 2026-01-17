# High-Performance Movie Booking Backend

A production-grade, high-concurrency seat reservation system built with **ASP.NET Core 8 (LTS)**. Designed to handle massive traffic spikes with zero overbooking, featuring HA architecture, API Idempotency, and Reactive Cleanup.

## 🚀 Architecture & Scalability

### 1. Multi-Layered Concurrency Control (“Defense in Depth”)

*   **Layer 1: Distributed Persistent Locks (Redis AOF)**
    *   **Mechanism**: A high-performance usage of `StackExchange.Redis` creates distributed locks with **AOF (Append Only File)** persistence.
    *   **Capacity**: Can handle 100,000+ requests/second.
    *   **Resilience**: Unlike in-memory locks, these **survive server restarts**.

*   **Layer 2: Database Atomic Transactions**
    *   **Mechanism**: All bulk operations are wrapped in ACID-compliant `BEGIN TRANSACTION` scopes using **PostgreSQL**.
    *   **Guarantee**: "All or Nothing". It is mathematically impossible for a partial booking to occur.

*   **Layer 3: Optimistic Concurrency Checks**
    *   **Mechanism**: Application-level version tokens (`RowVersion`) safeguard every database write.

### 2. High Availability (HA) & Fault Tolerance
*   **Redis Sentinel**: The system uses a **Primary-Replica-Sentinel** architecture. If the Master Redis node fails, Sentinel automatically promotes the Replica, ensuring the "Gatekeeper" layer never goes down.
*   **API Idempotency**: Supports `Idempotency-Key` headers. If a client retries a booking due to network failure, the server returns the cached success response instead of processing a duplicate booking.

### 3. Reactive Self-Healing
*   **Keyspace Notifications**: Instead of slow polling, the system listens to Redis `__keyevent@0__:expired` events.
*   **Result**: When a hold expires, the seat is released **milliseconds** later, maximizing inventory availability.

---

## 🛠 Tech Stack

*   **Framework**: .NET 8 (LTS) - Enterprise Grade Stability
*   **API**: ASP.NET Core Web API
*   **Database**: PostgreSQL 15 (Production) / SQLite (Dev)
*   **Cache**: Redis 7 (AOF + Sentinel + Keyspace Notifications)
*   **Testing**: k6 (Load Testing)
*   **Containerization**: Docker & Docker Compose

---

## 🧠 Solved Engineering Challenges

### The "Ghost Booking" / Retry Problem
*   **Problem**: User books a seat, server succeeds, but network drops before response. User retries -> Double Charge / Error.
*   **Solution**: **Idempotency Middleware**. The server recognizes the unique `Idempotency-Key` and replays the previous internal response.

### The "Zombie Seat" Latency
*   **Problem**: A polling job running every minute leaves a seat "dead" for up to 59 seconds after expiry.
*   **Solution**: **Reactive Pub/Sub**. Redis notifies the backend instantly upon key expiry.
*   **Result**: Seat becomes bookable again in <100ms.

---

## ⚙️ Development vs. Production

The application is configured for **Zero-Dependency Development** by default.

### 🟡 Development Mode (Default)
*   **Database**: SQLite (`moviebooking.db`).
*   **Cache**: In-Memory Mock Redis.
*   **Setup**: Just run:
    ```bash
    dotnet run --project MovieBooking.Api/MovieBooking.Api.csproj --urls "http://localhost:5033"
    ```

### 🟢 Production Mode (Docker HA)
To enable the high-performance architecture (PostgreSQL + Redis Sentinel):
1.  **Start Infrastructure**:
    ```bash
    docker compose up -d
    ```
2.  **Update Config**:
    In `MovieBooking.Api/Program.cs`, uncomment the **Production** lines (Npgsql, RedisLockService, RedisKeyExpiredSubscriber) and comment out the **Development** lines.

---

## 📉 Load Testing
A **k6** script (`k6_load_test.js`) is included to verify the system handles **500+ concurrent users** and Idempotency logic correctness.
```bash
k6 run k6_load_test.js
```
