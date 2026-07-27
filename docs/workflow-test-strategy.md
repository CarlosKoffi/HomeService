# Workflow test strategy

The mission workflow is protected by two complementary test layers.

## CI integration workflows

GitHub Actions runs the integration test project after every Release build. These tests use an isolated in-memory database and never call real payment or notification providers. Payment is mocked and Firebase/email/WhatsApp delivery is disabled in CI; only the notification records/outbox are asserted.

Covered critical paths:

- client request with photos
- company offer acceptance
- provider assignment
- provider mobile notification outbox
- provider accept/refuse workflow
- client quote acceptance with mocked payment reference
- contact release after payment authorization
- arrival verification with GPS tolerance
- mission start and completion
- customer completion validation and rating
- company payout accounting
- cancellation and refund accounting
- dispute resolution and refund decision
- additional quote request, submission and mocked payment
- no real external notification dispatch during automated tests

Notification checks verify that portal notifications and mobile push outbox messages are created with the right workflow timing and remain pending instead of being sent for real.

## Post-deploy smoke checks

The deployed smoke test is intentionally non-destructive. It verifies the API is alive and that critical admin/workflow endpoints, notification rules and notification templates are available after migrations and seeders have run.

The smoke script must fail on HTTP 404, 500 or 502. Those failures mean the deployed app or database schema is not aligned with the code.

## Real providers

Real payment capture and real Firebase/email/WhatsApp dispatch stay outside automated CI tests. They should be validated later on a dedicated staging environment with test provider credentials and disposable accounts.
