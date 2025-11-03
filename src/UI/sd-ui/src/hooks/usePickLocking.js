import { useState, useEffect } from 'react';

/**
 * Custom hook to manage pick locking based on game start time and user permissions
 * @param {string} startDateUtc - Game start date in UTC
 * @param {boolean} isReadOnly - Whether user is in read-only mode
 * @returns {object} { isLocked, lockTime }
 */
export const usePickLocking = (startDateUtc, isReadOnly) => {
  const [now, setNow] = useState(new Date());

  useEffect(() => {
    const interval = setInterval(() => {
      setNow(new Date());
    }, 15000); // check every 15 seconds

    return () => clearInterval(interval);
  }, []);

  if (!startDateUtc) {
    return { isLocked: isReadOnly, lockTime: null };
  }

  // Picks are locked 5 minutes prior to kickoff OR if user is read-only
  const startTime = new Date(startDateUtc);

  if (isNaN(startTime.getTime())) {
    return { isLocked: isReadOnly, lockTime: null };
  }

  const lockTime = new Date(startTime.getTime() - 5 * 60 * 1000); // subtract 5 minutes
  const isLocked = now > lockTime || isReadOnly;

  return { isLocked, lockTime };
};
