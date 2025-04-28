
import { FaSpinner } from "react-icons/fa";

function SubmitButton({ submitted, isSubmitting, onSubmit }) {
  const handleMouseOver = (e) => {
    if (!submitted && !isSubmitting) {
      e.currentTarget.style.backgroundColor = "#00b34a";
    }
  };

  const handleMouseOut = (e) => {
    if (!submitted && !isSubmitting) {
      e.currentTarget.style.backgroundColor = "#00c853";
    }
  };

  return (
    <button
      onClick={onSubmit}
      disabled={submitted || isSubmitting}
      style={{
        backgroundColor: submitted ? "#555" : "#00c853",
        color: "#fff",
        border: "none",
        padding: "10px 20px",
        fontSize: "1.2rem",
        borderRadius: "8px",
        cursor: submitted ? "not-allowed" : "pointer",
        fontWeight: "bold",
        transition: "background-color 0.3s",
      }}
      onMouseOver={handleMouseOver}
      onMouseOut={handleMouseOut}
    >
      {isSubmitting ? (
        <span className="submitting-content">
          <FaSpinner className="spinner" /> Submitting...
        </span>
      ) : submitted ? (
        "Picks Submitted!"
      ) : (
        "Submit Picks"
      )}
    </button>
  );
}

export default SubmitButton;
