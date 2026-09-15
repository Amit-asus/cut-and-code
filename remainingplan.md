


# Notificatin system remaining 
# using the asure service bus Bus 
Plan, locked in
apphost.cs — add Azure Service Bus emulator + a booking-created queue, wire it to booking-api and notification-api.
Booking.Api — add a Services/BookingEventPublisher.cs (or similar), call it after a successful booking create.
Identity.Api — add GET /internal/users/{id} (no auth), returns just { id, email }.
Salon.Api — add GET /internal/salons/{salonId}/services/{serviceId} (no auth) returning { salonName, serviceName } (or split into two calls if simpler — we can decide when we get there).
Notification.Api — build it out for real: BackgroundService Service Bus consumer, calls to the two internal endpoints, SendGrid send. This is the biggest chunk.
SendGrid — you sign up + get an API key, we store it via dotnet user-secrets (matching the Jwt:SigningKey pattern) or as an apphost env var like the admin seed password — your call when we get there.
Test end-to-end: create a booking → confirm a Service Bus message goes out (visible in Aspire dashboard) → confirm Notification.Api picks it up → confirm a real email lands in your inbox.

//plan locked in the conversation Name : Frontend React build 