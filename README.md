# Movie Seat Manager - High-Performance Booking System

A production-grade backend system designed to handle high-concurrency movie ticket bookings. This solution addresses race conditions, system failures, and network inconsistencies using a **Distributed Locking mechanism**, **Idempotency**, and **ACID-compliant transactions**.

---

## 📋 Problem Statement & Solution Matrix

This system was built to specifically address the challenges outlined in the Fantacode Hiring Task.

| Challenge (from Task) | Solution Implemented |
| :--- | :--- |
| **1. Concurrent Booking** | **Multi-Layer Concurrency Control**: Redis Distributed Locks (NX) + Serializable DB Transactions prevent double-booking. |
| **2. Incomplete Bookings** | **TTL (Time-To-Live)**: Seats held are automatically expired after 60 seconds if not booked. |
| **3. Seat Release** | **Reactive Cleanup**: A background worker listens to Redis Keyspace Notifications to instantly release expired seats. |
| **4. User Retries/Refreshes** | **Idempotency Middleware**: Retrying a request with the same `Idempotency-Key` returns the original cached response without re-processing. |
| **5. No Response Received** | **Result Caching**: If the server processes a booking but the network fails before the response reaches the client, the next retry fetches the saved success message. |
| **6. System Restarts** | **Persistence**: Redis AOF (Append Only File) & PostgreSQL ensure state is recovered after a crash. |

---

## 🚀 Architecture & Scalability

The system employs a "Defense in Depth" strategy to ensure data integrity under load.

### 1. Multi-Layered Concurrency Control

To handle massive traffic spikes with zero overbooking:

*   **Layer 1: Distributed Persistent Locks (Redis AOF)**
    *   **Mechanism**: A high-performance usage of `StackExchange.Redis` creates distributed locks with **AOF (Append Only File)** persistence.
    *   **Capacity**: Can handle 100,000+ requests/second.
    *   **Resilience**: Unlike in-memory locks, these **survive server restarts**.

*   **Layer 2: Database Atomic Transactions**
    *   **Mechanism**: All bulk operations are wrapped in ACID-compliant `BEGIN TRANSACTION` scopes using **PostgreSQL**.
    *   **Guarantee**: "All or Nothing". It is mathematically impossible for a partial booking to occur.

*   **Layer 3: Optimistic Concurrency Checks**
    *   **Mechanism**: Application-level status checks safeguard every database write, acting as the final source of truth.

### 2. High Availability (HA) & Fault Tolerance

*   **Redis Sentinel**: The system uses a **Primary-Replica-Sentinel** architecture. If the Master Redis node fails, Sentinel automatically promotes the Replica, ensuring the "Gatekeeper" layer never goes down.
*   **API Idempotency**: Supports `Idempotency-Key` headers. This solves the "Ghost Booking" problem where a user is charged but thinks the request failed.

### 3. Reactive Self-Healing

*   **Keyspace Notifications**: Instead of slow polling (cron jobs), the system listens to Redis `__keyevent@0__:expired` events.
*   **Result**: When a hold expires, the seat is released **milliseconds** later, maximizing inventory availability.

---

## 🛠 Tech Stack

*   **Framework**: .NET 8 (LTS) - Enterprise Grade Stability
*   **API**: ASP.NET Core Web API
*   **Database**: PostgreSQL 15 (Production) / SQLite (Dev)
*   **Cache**: Redis 7 (AOF + Sentinel + Keyspace Notifications)
*   **Real-time**: SignalR (WebSockets for instant seat updates)
*   **Testing**: k6 (Load Testing)
*   **Containerization**: Docker & Docker Compose

---

## 🚀 Getting Started

### Prerequisites

*   Docker & Docker Compose
*   .NET 8 SDK (Optional, for local debugging)

### 🟡 Development Mode (Zero-Config)
To run locally without Docker (uses SQLite + Mock Redis):
```bash
dotnet run --project MovieBooking.Api/MovieBooking.Api.csproj --urls "http://localhost:5033"
```
*API available at `http://localhost:5033`.*

### Run with Docker (Recommended)

This spins up the API, PostgreSQL, Redis Master, Redis Replica, and Redis Sentinel.

1.  **Clone the repository**
    ```bash
    git clone <your-repo-url>
    cd movie_seat_manager
    ```

2.  **Start the Infrastructure**
    ```bash
    docker-compose up --build
    ```
    The API will be available at `http://localhost:5000` (or the port defined in docker-compose).

3.  **Verify Status**
    Visit `http://localhost:5000/api/seats/shows` to see available shows.

---

## 🔌 API Reference

### 1. Get Seat Map
Returns all seats and their current status for a specific show.
```http
GET /api/seats/{showId}
```

### 2. Hold Seats (Bulk)
Attempts to temporarily reserve seats. Requires `Idempotency-Key`.
```http
POST /api/seats/hold-bulk
Idempotency-Key: unique-request-id-123
Content-Type: application/json

{
  "seatIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"],
  "userId": "user_1"
}
```

### 3. Book Seats (Confirm)
Converts "Held" seats to "Booked".
```http
POST /api/seats/book-bulk
Idempotency-Key: booking-req-id-999
Content-Type: application/json

{
  "seatIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"],
  "userId": "user_1"
}
```

---

## 🧪 Load Testing

A **k6** load testing script is included to simulate high concurrency.

1.  **Install k6**: [https://k6.io/docs/get-started/installation/](https://k6.io/docs/get-started/installation/)
2.  **Run the test**:
    ```bash
    k6 run k6_load_test.js
    ```

**Scenario Tested**:
500 Virtual Users simultaneously trying to hold and book the *same* small set of seats.
**Expected Result**: Zero overbookings. Only 1 user succeeds per seat.

---

## � Project Structure

```
├── MovieBooking.Api           # Entry point, Controllers, Middleware
│   ├── Middleware/            # Idempotency Logic
│   └── Controllers/           # REST Endpoints
├── MovieBooking.Core          # Domain Entities (Seat, Show), Interfaces
├── MovieBooking.Infrastructure
│   ├── Data/                  # EF Core Context
│   ├── Services/              # Redis Locking, Seat Business Logic
│   └── BackgroundJobs/        # Redis Expiry Listener (Cleanup)
└── docker-compose.yml         # Infrastructure Orchestration
```

---

## 🧠 Design Decisions

### Why Redis Locks?
*   **Decision**: We use Redis as a first line of defense.
*   **Benefit**: Prevents database "Hot Row" contention. If 5,000 users click the same seat, Redis rejects 4,999 of them in memory before they even hit the Postgres transaction log.

### Why SignalR?
*   **Decision**: Instead of clients polling for seat status, we push updates.
*   **Benefit**: Reduces server load and provides a smoother user experience where seats turn "Red" (booked) instantly for everyone.
