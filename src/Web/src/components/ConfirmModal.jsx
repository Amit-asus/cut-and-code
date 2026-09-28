import { useEffect, useRef } from "react";

// Native <dialog> + showModal() gives us focus trapping, Esc-to-close and a backdrop for free.
export function ConfirmModal({
  open,
  title,
  message,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  busy = false,
  onConfirm,
  onCancel,
}) {
  const dialogRef = useRef(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  function handleCancelEvent(e) {
    // Fired on Esc — keep React state as the source of truth instead of letting the browser close it.
    e.preventDefault();
    if (!busy) onCancel();
  }

  function handleBackdropClick(e) {
    // A click whose target is the <dialog> itself landed on the backdrop, not the content.
    if (e.target === dialogRef.current && !busy) onCancel();
  }

  return (
    <dialog ref={dialogRef} className="modal" onCancel={handleCancelEvent} onClick={handleBackdropClick}>
      <div className="modal-body">
        <h2 className="modal-title">{title}</h2>
        <p className="modal-message">{message}</p>
        <div className="modal-actions">
          <button type="button" className="btn btn-outline" onClick={onCancel} disabled={busy}>
            {cancelLabel}
          </button>
          <button type="button" className="btn btn-danger" onClick={onConfirm} disabled={busy}>
            {busy ? "Please wait…" : confirmLabel}
          </button>
        </div>
      </div>
    </dialog>
  );
}
