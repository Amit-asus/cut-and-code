import { useEffect, useRef } from "react";

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
    e.preventDefault();
    if (!busy) onCancel();
  }

  function handleBackdropClick(e) {
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
