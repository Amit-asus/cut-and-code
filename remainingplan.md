


# Notification system remaining
# using the azure service bus
Plan, locked in

Status (2026-09-23): DONE. All 5 milestones complete and verified end-to-end,
including real SendGrid email delivery to a real inbox. This plan is closed out.

- [x] apphost.cs — Azure Service Bus emulator + booking-created queue added.
      booking-api and notification-api both reference the `serviceBus` (namespace)
      resource directly — NOT the queue resource — since AddAzureServiceBusClient("servicebus")
      looks up ConnectionStrings:servicebus. (Hit and fixed: referencing the queue alone
      threw "A ServiceBusClient could not be configured" at request time.)
      notification-api also references identityApi and salonApi directly (needed to
      resolve https://identity-api / https://salon-api for the internal HTTP calls below).
- [x] Booking.Api — Services/BookingEventPublisher.cs added, called from POST /bookings
      after a successful save. Wrapped in try/catch (ServiceBusException), same
      best-effort/non-blocking philosophy as the existing Redis cache calls — a Service Bus
      outage should never fail a booking.
- [x] Identity.Api — GET /internal/users/{id} (no auth), returns { id, email }. Tested directly.
- [x] Salon.Api — GET /internal/salons/{salonId}/services/{serviceId} (no auth), returns
      { salonName, serviceName }, 404s if the service doesn't belong to that salon. Tested directly.
- [x] Notification.Api — built out for real:
      Models/BookingCreatedMessage.cs (mirrors the publisher's payload shape)
      Services/BookingEnrichmentService.cs (calls both internal endpoints via a named
        HttpClient, Aspire service discovery resolves the https://identity-api /
        https://salon-api pseudo-hosts same as the Gateway's YARP config)
      Services/EmailSender.cs — STUB ONLY, logs "[EMAIL STUB] To: ..." instead of sending.
        Swap this file's body for a real SendGrid call when picking Milestone 5 back up;
        its public method signature is intentionally already final so nothing else changes.
      Services/BookingNotificationConsumer.cs — a real BackgroundService, processes the
        booking-created queue for the app's lifetime, only completes a message after
        successful processing (so a failure leaves it for retry, not silently dropped).
      Removed the /weatherforecast scaffold entirely.
      Gotcha hit and fixed: do NOT add an explicit Microsoft.Extensions.ServiceDiscovery
      PackageReference here — SalonBooking.ServiceDefaults already pulls it in transitively
      at a newer version (10.8.0 at time of writing); an explicit older reference triggers
      NU1605 "Detected package downgrade" as a build error. AddServiceDefaults() already
      wires service discovery into HttpClientFactory-created clients for free.
- [x] SendGrid (Milestone 5) — signed up, generated a restricted (Mail Send only) API
      key, stored via `dotnet user-secrets set "SendGrid:ApiKey" "..."` from
      Notification.Api's project directory (matches the Jwt:SigningKey pattern, never
      committed). SendGrid:FromEmail/FromName are non-secret, live in appsettings.json
      (amit.dev.kushwaha@gmail.com). EmailSender.cs now builds a real message via
      SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail and sends via SendGridClient;
      logs a warning with SendGrid's response body on failure instead of throwing (a
      failed send shouldn't crash the consumer or dead-letter the message pointlessly).
      Gotcha hit and fixed: first attempt failed with 403 Forbidden — "The from address
      does not match a verified Sender Identity" — because Single Sender Verification
      for amit.dev.kushwaha@gmail.com had silently ended in a `failed` status (the
      confirmation link either wasn't clicked in time or didn't arrive). Re-created the
      sender in SendGrid's dashboard (Settings → Sender Authentication) and promptly
      clicked the verification email this time — status flipped to Verified and sends
      started working immediately with no code changes needed.
- [x] Test end-to-end (2026-09-23): created a booking through the app → Booking.Api
      published to Service Bus (confirmed in booking-api's own logs: SendAsync start/done)
      → Notification.Api consumed it, called both internal endpoints successfully (200s
      logged) → SendGrid accepted and delivered the email → confirmed received in the
      real recipient inbox. Full pipeline confirmed working end to end, nothing stubbed.

//plan locked in the conversation Name : Frontend React build 

//This Week Plan 

1. check the  gateway routing 
2. check how can we do async communication 
3. use the sendgrid to send the email notification 
4. load balancing 

//This week 
1. will have the load balancing 
2. rate limiting 
3. can do payment integration  (stripe integration)
4.  Review/feedback summarization
You don't have a reviews feature yet, so this would require building reviews first — bigger lift, skip unless you want reviews anyway.
5. 2. AI booking assistant / natural-language search ("chatbot" style)
"Find me a haircut salon near Koramangala under ₹600 available tomorrow evening" → the LLM translates that into a query against your existing /salons and /bookings/availability data. This is the most "impressive demo" option but the most work — needs a new endpoint, likely a new small service or just a feature inside the Gateway/frontend, and prompt engineering to reliably turn natural language into structured filters.