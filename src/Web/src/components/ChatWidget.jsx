import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";

const SUGGESTIONS = [
  "What is the cancellation policy?",
  "Which salons offer a haircut?",
  "Book a haircut for tomorrow",
];

const GREETING = {
  role: "assistant",
  text: "Hi! I can answer questions about our salons, services and policies, and help you book a slot.",
  sources: [],
  answered: true,
};

const HISTORY_LIMIT = 10;

function formatSlot(startTime) {
  return new Date(startTime).toLocaleString("en-IN", {
    timeZone: "Asia/Kolkata",
    weekday: "short",
    day: "numeric",
    month: "short",
    hour: "numeric",
    minute: "2-digit",
  });
}

function BookingCard({ proposal, status, error, onConfirm, onNavigate }) {
  return (
    <div className="chat-booking">
      <p className="chat-booking-service">{proposal.serviceName}</p>
      <p className="chat-booking-detail">{proposal.salonName}</p>
      <p className="chat-booking-detail">
        {formatSlot(proposal.startTime)} · ₹{proposal.price}
      </p>

      {status === "booked" ? (
        <p className="chat-booking-done">
          Booked ✓{" "}
          <Link to="/bookings" onClick={onNavigate}>
            View My Bookings
          </Link>
        </p>
      ) : status === "error" ? (
        <p className="chat-booking-error">{error}</p>
      ) : (
        <button type="button" className="btn chat-booking-confirm" onClick={onConfirm} disabled={status === "booking"}>
          {status === "booking" ? "Booking..." : "Confirm booking"}
        </button>
      )}
    </div>
  );
}

function ChatIcon() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
      <path d="M18 6 6 18M6 6l12 12" />
    </svg>
  );
}

function AnswerText({ text }) {
  const parts = text.split(/(\[\d+\])/g);
  return (
    <p className="chat-text">
      {parts.map((part, index) =>
        /^\[\d+\]$/.test(part) ? (
          <span key={index} className="chat-cite">
            {part.slice(1, -1)}
          </span>
        ) : (
          part
        )
      )}
    </p>
  );
}

function Sources({ sources, onNavigate }) {
  if (!sources?.length) {
    return null;
  }

  return (
    <ol className="chat-sources">
      {sources.map((source, index) => (
        <li key={`${source.title}-${index}`} className="chat-source">
          <span className="chat-cite">{index + 1}</span>
          {source.url ? (
            <Link to={source.url} onClick={onNavigate} className="chat-source-title">
              {source.title}
            </Link>
          ) : (
            <details>
              <summary className="chat-source-title">{source.title}</summary>
              <p className="chat-source-snippet">{source.snippet}</p>
            </details>
          )}
        </li>
      ))}
    </ol>
  );
}

export function ChatWidget() {
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState([GREETING]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const inputRef = useRef(null);
  const listRef = useRef(null);

  useEffect(() => {
    if (open) {
      inputRef.current?.focus();
    }
  }, [open]);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, loading]);

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    function handleKey(event) {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }
    window.addEventListener("keydown", handleKey);
    return () => window.removeEventListener("keydown", handleKey);
  }, [open]);

  async function ask(question) {
    const trimmed = question.trim();
    if (!trimmed || loading) {
      return;
    }

    const history = messages
      .slice(1)
      .filter((message) => message.text)
      .slice(-HISTORY_LIMIT)
      .map(({ role, text }) => ({ role, text }));

    setMessages((current) => [...current, { role: "user", text: trimmed }]);
    setInput("");
    setLoading(true);

    try {
      const result = await api.askAssistant(trimmed, history);
      setMessages((current) => [
        ...current,
        {
          role: "assistant",
          text: result.answer,
          sources: result.sources,
          answered: result.answered,
          proposal: result.proposal ?? null,
          proposalStatus: result.proposal ? "pending" : null,
        },
      ]);
    } catch (error) {
      const tooMany = /too many|429/i.test(error.message);
      setMessages((current) => [
        ...current,
        {
          role: "assistant",
          text: tooMany
            ? "You're asking questions quickly. Please wait a minute and try again."
            : "Sorry, I couldn't reach the assistant right now. Please try again shortly.",
          sources: [],
          answered: false,
        },
      ]);
    } finally {
      setLoading(false);
    }
  }

  function updateMessage(index, patch) {
    setMessages((current) => current.map((message, i) => (i === index ? { ...message, ...patch } : message)));
  }

  async function confirmBooking(index) {
    const { proposal } = messages[index];
    updateMessage(index, { proposalStatus: "booking" });

    try {
      await api.createBooking({
        salonId: proposal.salonId,
        serviceId: proposal.serviceId,
        startTime: proposal.startTime,
        endTime: proposal.endTime,
      });
      updateMessage(index, { proposalStatus: "booked" });
      setMessages((current) => [
        ...current,
        {
          role: "assistant",
          text: `You're booked: ${proposal.serviceName} at ${proposal.salonName}, ${formatSlot(proposal.startTime)}. A confirmation email is on its way.`,
          sources: [],
          answered: true,
        },
      ]);
    } catch (error) {
      updateMessage(index, {
        proposalStatus: "error",
        proposalError: `Couldn't book this slot (${error.message}). Ask me for another time.`,
      });
    }
  }

  function handleSubmit(event) {
    event.preventDefault();
    ask(input);
  }

  const showSuggestions = messages.length === 1 && !loading;

  return (
    <div className="chat-widget">
      {open && (
        <section className="chat-panel" role="dialog" aria-label="Support assistant">
          <header className="chat-header">
            <div>
              <span className="label">Support</span>
              <h2 className="chat-title">Ask Aurelia</h2>
            </div>
            <button type="button" className="btn btn-ghost chat-close" onClick={() => setOpen(false)} aria-label="Close assistant">
              <CloseIcon />
            </button>
          </header>

          <div className="chat-messages" ref={listRef} aria-live="polite">
            {messages.map((message, index) => (
              <div key={index} className={`chat-message chat-${message.role} ${message.answered === false ? "is-fallback" : ""}`}>
                {message.role === "assistant" ? <AnswerText text={message.text} /> : <p className="chat-text">{message.text}</p>}
                {message.role === "assistant" && <Sources sources={message.sources} onNavigate={() => setOpen(false)} />}
                {message.proposal && (
                  <BookingCard
                    proposal={message.proposal}
                    status={message.proposalStatus}
                    error={message.proposalError}
                    onConfirm={() => confirmBooking(index)}
                    onNavigate={() => setOpen(false)}
                  />
                )}
              </div>
            ))}

            {loading && (
              <div className="chat-message chat-assistant chat-typing" aria-label="Assistant is typing">
                <span />
                <span />
                <span />
              </div>
            )}

            {showSuggestions && (
              <div className="chat-suggestions">
                {SUGGESTIONS.map((suggestion) => (
                  <button key={suggestion} type="button" className="chat-suggestion" onClick={() => ask(suggestion)}>
                    {suggestion}
                  </button>
                ))}
              </div>
            )}
          </div>

          <form className="chat-form" onSubmit={handleSubmit}>
            <input
              ref={inputRef}
              value={input}
              onChange={(event) => setInput(event.target.value)}
              placeholder="Ask a question..."
              maxLength={500}
              aria-label="Your question"
              disabled={loading}
            />
            <button type="submit" className="btn" disabled={loading || !input.trim()}>
              Send
            </button>
          </form>
          <p className="chat-disclaimer">Answers come from our help articles and live salon data. Nothing is booked until you press Confirm.</p>
        </section>
      )}

      <button
        type="button"
        className="chat-launcher"
        onClick={() => setOpen((value) => !value)}
        aria-label={open ? "Close support assistant" : "Open support assistant"}
        aria-expanded={open}
      >
        {open ? <CloseIcon /> : <ChatIcon />}
      </button>
    </div>
  );
}
