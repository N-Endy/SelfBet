# SelfBet

Automated sports betting assistant for SportyBet Nigeria — analyses today's football fixtures, builds two disjoint accumulators per day targeting 6–10× total odds, and either generates a **booking code** (you tap once in the app) or places the slip automatically.

---

## How it works

```
08:00 & 16:00 (Africa/Lagos)
  └── Fetch live fixtures + odds from SportyBet public API (143+ matches/day)
  └── Poisson xG model blends bookmaker probabilities
  └── SlipOptimizer builds 2 disjoint slips within target odds range
  └── SportyBetBookingClient → POST /api/ng/orders/share → booking code
  └── Email notification with booking code + deep link
  └── Data saved to Neon Postgres (persists forever)
```

### Placement modes

| Mode | How it works | When to use |
|---|---|---|
| `booking_code` (default) | Generates a 5-char share code. Open link or enter code in SportyBet app → tap Place. | Always safe, no credentials stored. |
| `full_auth` | Logs into your account, calls orders API, stakes immediately. | Set `SPORTYBET__PLACEMENTMODE=full_auth` once comfortable. |

---

## Quick start (local)

### Prerequisites
- .NET 10 SDK
- Docker (optional, for local Postgres)

### Run with Neon

```bash
# 1. Copy environment file
cp .env.example .env
# Fill in your credentials in .env

# 2. Start API
dotnet run --project src/SelfBet.Api -- \
  --ConnectionStrings:DefaultConnection="<your neon connection string>"

# 3. Start dashboard (separate terminal)
dotnet run --project src/SelfBet.Dashboard

# 4. Open http://localhost:5080 (API) and http://localhost:5164 (Dashboard)
# 5. Click "Generate Now" on the dashboard home page
```

On first startup, EF Core automatically applies the database migration to Neon.

---

## Deploy to Render (recommended for 24/7 operation)

This deploys both services to Render's free tier with Neon Postgres.

### One-time setup

1. **Push to GitHub**
   ```bash
   git init && git add . && git commit -m "initial"
   gh repo create selfbet --private --push --source=.
   ```

2. **Create a Render account** at [render.com](https://render.com) → sign in with GitHub.

3. **New Blueprint** → point it to your GitHub repo → Render reads `render.yaml` automatically.

4. **Set secret environment variables** in the Render dashboard for `selfbet-api`:
   

5. **Deploy** → Render builds both Docker images, API migrates the DB on startup.

6. **Open the dashboard URL** on your phone — it works in any Android browser, pinnable to home screen as a PWA.

---

## Using booking codes

After each run you'll receive an email like:

```
SelfBet — 2 slips ready (Mon 27 Apr)

Slip 1 — 9.45× odds   Stake: ₦200 → Win: ₦1,890

  T1KSRU   [Copy]  [Open in SportyBet app]

  KR Reykjavik vs FH Hafnarfjordur  |  Under 2.5  |  4.50
  Gil Vicente Barcelos vs Casa Pia  |  BTTS Yes   |  2.10
```

To place:
1. Open SportyBet → **More** → **Booking Code** → enter `T1KSRU` → Place Bet.
2. Or tap "Open in SportyBet app" from the email / dashboard — the slip loads instantly.

---

## Enabling full automation (stakes placed without tapping)

1. Set `SPORTYBET__PLACEMENTMODE=full_auth` in your Render env vars.
2. In the dashboard go to Config → tick **Automation Enabled** → Save.
3. The next scheduled run will log in, fetch balance, and place both slips automatically.

On first login, SportyBet sends an SMS OTP. The dashboard shows an OTP entry modal — enter the code to unblock. Subsequent runs reuse the session cookie (~12 hours).

---

## Key configuration (dashboard Config page)

| Setting | Default | Description |
|---|---|---|
| Min / Max total odds | 6 / 10 | Accumulator total odds target range |
| Stake % per slip | 2% | Fraction of bankroll staked per slip |
| Slips per day | 2 | Number of disjoint accumulators |
| Min / Max leg odds | 1.20 / 4.50 | Per-selection odds filter |
| Min edge threshold | 2% | Model must exceed book by ≥2% |
| Enabled leagues | EPL, La Liga, … | 17 leagues enabled by default |
| Allowed markets | 1X2, BTTS, O/U 2.5, DoubleChance, DrawNoBet | Which bet types to consider |

---

## Architecture

```
SelfBet.Domain          — Entities, enums, value objects (no dependencies)
SelfBet.Application     — Use cases, services, interfaces (depends on Domain)
SelfBet.Infrastructure  — EF Core/Neon, SportyBet data provider, SMTP, calibration
SelfBet.Automation      — SportyBetBookingClient, SportyBetAuthClient, gateway
SelfBet.Api             — Minimal API, background scheduler worker
SelfBet.Dashboard       — Blazor Server interactive dashboard
```

### Prediction pipeline

1. `SportyBetMarketDataProvider` — fetches live odds (1X2 + O/U 2.5 + BTTS) from SportyBet's public API, derives DoubleChance and DrawNoBet.
2. `FeatureBuilder` — turns raw odds into feature vectors (market-implied probability, derived xG features).
3. `PoissonPredictionService` — Poisson goal-expectation model blended with bookmaker prior (50/50 initially). After ≥50 settled observations, `CalibrationService` applies Platt scaling per market.
4. `SlipOptimizer` — target-aware greedy algorithm. Builds N disjoint slips, picking legs whose cumulative odds land closest to the midpoint of the target range.
5. `SafetyGate` — blocks or flags unusual runs.
6. `SportyBetAutomationGateway` — booking-code or full-auth placement.

---

## Bankroll tracking

- Seeded at ₦10,000 on first run.
- In `booking_code` mode: after you place a slip and it settles, update the balance manually on the dashboard Bankroll page, or use `/api/bankroll/snapshot`.
- In `full_auth` mode: the API reads your live balance from SportyBet before each run and persists it automatically.

---

## Secrets (never commit)

All sensitive values live in `.env` (local) or Render environment variables (production). The `.env` file is in `.gitignore`.
