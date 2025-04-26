import { useState } from 'react';
import matchups from '../../data/matchups.js';
import MatchupCard from '../matchups/MatchupCard.js';
import toast from 'react-hot-toast';
import { FaSpinner } from 'react-icons/fa';
import './PicksPage.css';

function PicksPage() {

  const [userPicks, setUserPicks] = useState({});
  const [submitted, setSubmitted] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  function handlePick(matchupId, teamPicked) {
    setUserPicks(prev => ({
      ...prev,
      [matchupId]: teamPicked
    }));
  }

  function handleSubmit() {

    if (isSubmitting) return; // Prevent double submission

    setIsSubmitting(true); // Start loading spinner

    setTimeout(() => {
        console.log('User Picks:', userPicks);
    
        // Fake "API call" effect (could add setTimeout to simulate real async later)
        toast.success('Your picks have been submitted!');
        // Later: Actually POST picks to your API here
    
        setIsSubmitting(false); // Stop loading
    
        setSubmitted(true); // <-- disable after submit
    }, 1500);
  }

    const currentWeek = 7;
    const weekStartDate = "October 19, 2025";
    const weekEndDate = "October 21, 2025";

  return (
    <div>
        <h2>
            Week {currentWeek} Picks
            <div className="week-dates">{weekStartDate} - {weekEndDate}</div>
        </h2>
        {matchups.map(m => (
            <MatchupCard 
            key={m.id} 
            matchup={m} 
            userPick={userPicks[m.id]} 
            onPick={handlePick}
            />
        ))}

        <div style={{ marginTop: '30px', textAlign: 'center' }}>
            <button 
                onClick={handleSubmit} 
                disabled={submitted || isSubmitting}
                style={{
                backgroundColor: submitted ? '#555' : '#00c853',
                color: '#fff',
                border: 'none',
                padding: '10px 20px',
                fontSize: '1.2rem',
                borderRadius: '8px',
                cursor: submitted ? 'not-allowed' : 'pointer',
                fontWeight: 'bold',
                transition: 'background-color 0.3s'
                }}
                onMouseOver={(e) => {
                if (!submitted && !isSubmitting) e.currentTarget.style.backgroundColor = '#00b34a';
                }}
                onMouseOut={(e) => {
                if (!submitted && !isSubmitting) e.currentTarget.style.backgroundColor = '#00c853';
                }}
            >
                {isSubmitting ? (
                <span className="submitting-content">
                    <FaSpinner className="spinner" /> Submitting...
                </span>
                ) : submitted ? (
                'Picks Submitted!'
                ) : (
                'Submit Picks'
                )}
            </button>
        </div>
    </div>
  );
}

export default PicksPage;
