import { useState } from "react";
import "./ConfirmationDialog.css";

function ConfirmationDialog({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmText = "Confirm",
  cancelText = "Cancel",
  storageKey,
}) {
  const [dontAskAgain, setDontAskAgain] = useState(false);

  if (!isOpen) return null;

  const handleConfirm = () => {
    if (dontAskAgain && storageKey) {
      localStorage.setItem(storageKey, "true");
    }
    onConfirm();
  };

  return (
    <div className="confirmation-dialog-overlay">
      <div className="confirmation-dialog">
        <h3 className="confirmation-dialog-title">{title}</h3>
        <p className="confirmation-dialog-message">{message}</p>
        <div className="confirmation-dialog-checkbox">
          <input
            type="checkbox"
            id="dontAskAgain"
            checked={dontAskAgain}
            onChange={(e) => setDontAskAgain(e.target.checked)}
          />
          <label htmlFor="dontAskAgain">Do not ask me again</label>
        </div>
        <div className="confirmation-dialog-buttons">
          <button
            className="confirmation-dialog-button cancel"
            onClick={onClose}
          >
            {cancelText}
          </button>
          <button
            className="confirmation-dialog-button confirm"
            onClick={handleConfirm}
          >
            {confirmText}
          </button>
        </div>
      </div>
    </div>
  );
}

export default ConfirmationDialog; 