# The outbox publish leak — and two ways to fix it someday

**Status:** documented 2026-09-04, no action scheduled. Written for future
us after PR #724 nearly shipped a silently-inert admin endpoint.

## The leak

`_eventBus.Publish(...)` reads like fire-and-forget, but its actual
behavior is determined by two things invisible at the call site:

1. the ambient delivery mode (`IMessageDeliveryScope`), and
2. whether a `SaveChangesAsync` happens later on the same path.

Under the default bus-outbox mode, a publish is BUFFERED until a save
flushes it. On a path with no DbContext write, that save never comes:
the endpoint returns 202, the event never leaves the process, and
nothing anywhere complains. Classic leaky abstraction — the API's
signature lies about its semantics.

**The working rule (operator, verbatim):**
> No update to the dbContext? No messages sent; use direct.

i.e. every no-write publish must wrap in
`using (_deliveryScope.Use(DeliveryMode.Direct)) { ... }`.
Precedent with explanatory comment: `AdminController`'s
`ContestStatusChanged` publish. Near-miss: #724's backfill endpoint
shipped a bare publish (caught in review).

The known workarounds — the delivery-scope wrapper, or writing a dummy
record just to trigger a flush — both require the developer to already
know the mechanism. Convention is currently the only guardrail.

## Option 1 (preferred): make the call site confess

Split the API so the choice enters the type system:

- `PublishViaOutbox(...)` / `PublishDirect(...)`, or
- `Publish(evt, DeliveryMode mode)` with no default.

The developer still has to understand the mechanism — nothing removes
that — but the call site forces the question to be ASKED, which is the
difference between "must know" and "must remember." The ambient scope
becomes an implementation detail instead of a tripwire. Mechanical
refactor: every existing `Publish` call site gets the explicit mode
matching its current behavior; no runtime change.

## Option 2: make the failure loud

Dev-time detector at request/scope end: "bus outbox holds buffered
messages and no SaveChangesAsync occurred" → throw in Development,
error-log in Production. Turns the silent no-op into a failure the
first time anyone writes the bug. Cheaper than Option 1, catches the
symptom rather than preventing the mistake.

## Recommendation

Option 1 when the pain recurs or a second contributor joins; Option 2
is a reasonable interim if a quiet afternoon appears first. At solo
scale, the convention + hardened session memory may hold — this doc
exists so the next incident starts here instead of at another
investigation.
