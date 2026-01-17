# Movie Seat Manager - High-Performance Booking System

[![.NET 8](https://img.shields.io/badge/.NET%208-LTS-purple)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Compose-blue)](https://www.docker.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-lightgrey)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-7.0-red)](https://redis.io/)

A production-grade backend system designed to handle high-concurrency movie ticket bookings.  
The system prevents race conditions, handles retries safely, and guarantees consistency using **Distributed Locks**, **Idempotency**, and **ACID-compliant transactions**.

---

## 📋 Problem Statement & Solution Matrix

This system directly addresses the challenges outlined in the **Fantacode Hiring Task**.

| Challenge | Solution Implemented |
|---------|----------------------|
| Concurrent Booking | Redis Distributed Locks (`SET NX`) + `SERIALIZABLE` DB transactions |
| Incomplete Bookings | Redis TTL (60s) auto-expiry for held seats |
| Seat Release | Background worker using Redis Keyspace Notifications |
| System Restarts | Redis AOF + PostgreSQL persistence |
| Network Failures | Idempotency middleware with cached responses |

---

## 🏗️ Architecture Design

The system follows a **Defense-in-Depth** approach to concurrency and correctness.

### 1. Concurrency Guard (Double-Lock Pattern)

To safely handle 1000+ concurrent requests for the same seat:

1. **Redis Distributed Lock**
   - Key: `seat:{seatId}`
   - Acquired before DB access
   - Fails fast if seat is already locked

2. **Database Transaction**
   - Uses `SERIALIZABLE` isolation
   - Final authority on seat status

**Result**:  
Redis protects PostgreSQL from hot-row contention, while PostgreSQL remains the single source of truth.

---

### 2. Idempotency (Network Safety)

Clients may retry requests due to timeouts or refreshes.

- `IdempotencyMiddleware` intercepts `POST` requests
- Uses `Idempotency-Key` header
- Cached responses are returned for duplicate requests
- Prevents double booking and double charges

---

### 3. Real-Time Updates (SignalR)

- SignalR pushes seat status changes:
  - `Available → Held → Booked`
- Prevents stale UI interactions
- Improves user experience during high traffic

---

## 🛠️ Tech Stack

- **Backend**: ASP.NET Core 8 Web API
- **Database**: PostgreSQL 15 (Prod), SQLite (Dev)
- **Cache & Locks**: Redis 7 (AOF enabled)
- **Real-Time**: SignalR
- **Testing**: k6
- **Containerization**: Docker & Docker Compose

---

## 🚀 Getting Started

### Prerequisites
- Docker & Docker Compose
- .NET 8 SDK (optional for local dev)

### 🟡 Development Mode (Zero-Config)
To run locally without Docker (uses SQLite + Mock Redis):
```bash
dotnet run --project MovieBooking.Api/MovieBooking.Api.csproj --urls "http://localhost:5033"
```
*API available at `http://localhost:5033`.*

### Run with Docker (Recommended)

1. Clone the repository:
   ```bash
   git clone <your-repo-url>
   cd movie_seat_manager
   ```

2. Start all services:

   ```bash
   docker-compose up --build
   ```

3. Verify:

   ```
   http://localhost:5000/api/seats/shows
   ```

---

## 🔌 API Reference

### Get Seat Map

```http
GET /api/seats/{showId}
```

### Hold Seats (Bulk)

```http
POST /api/seats/hold-bulk
Idempotency-Key: unique-request-id
Content-Type: application/json

{
  "seatIds": ["uuid"],
  "userId": "user_1"
}
```

### Book Seats (Confirm)

```http
POST /api/seats/book-bulk
Idempotency-Key: booking-request-id
Content-Type: application/json

{
  "seatIds": ["uuid"],
  "userId": "user_1"
}
```

---

## 🧪 Load Testing

A k6 script simulates high concurrency.

```bash
k6 run k6_load_test.js
```

**Scenario**

* 500 virtual users booking the same seats
* Expected: **Zero overbookings**

---

## 📂 Project Structure

```
├── MovieBooking.Api
│   ├── Controllers
│   └── Middleware
├── MovieBooking.Core
│   └── Domain Entities
├── MovieBooking.Infrastructure
│   ├── Data
│   ├── Services
│   └── BackgroundJobs
└── docker-compose.yml
```

---

## 🧠 Design Decisions & Trade-offs

### Why Redis Locks?

* Prevents DB hot-row contention
* Rejects conflicting requests in-memory
* Trade-off: additional infrastructure

### Why Database Validation?

* Redis locks may expire
* Database transaction guarantees final correctness

### Why Redis AOF?

* Prevents losing lock state after crashes
* Ensures recovery consistency
