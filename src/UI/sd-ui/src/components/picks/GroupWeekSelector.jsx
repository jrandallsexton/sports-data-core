// src/components/picks/GroupWeekSelector.jsx
import "./GroupWeekSelector.css";

function GroupWeekSelector({ selectedGroup, setSelectedGroup, selectedWeek, setSelectedWeek }) {
    return (
      <div className="group-week-selector">
        {/* Group Select */}
        <div>
          <label htmlFor="groupSelect">Group:</label>
          <select
            id="groupSelect"
            value={selectedGroup}
            onChange={(e) => setSelectedGroup(e.target.value)}
          >
            <option value="Friends League">Friends League</option>
            <option value="Work Pool">Work Pool</option>
            <option value="Public Weekly Contest">Public Weekly Contest</option>
          </select>
        </div>
  
        {/* Week Select */}
        <div>
          <label htmlFor="weekSelect">Week:</label>
          <select
            id="weekSelect"
            value={selectedWeek}
            onChange={(e) => setSelectedWeek(Number(e.target.value))}
          >
            <option value={6}>Week 6</option>
            <option value={7}>Week 7</option>
            <option value={8}>Week 8</option>
          </select>
        </div>
      </div>
    );
  }
  

export default GroupWeekSelector;
