import './Wordmark.css';
import iconMark from '../../assets/sportdeets-icon-master.svg';

// Brand lockup: icon mark + two-tone italic wordmark.
//
// "sport" inherits --accent (cyan in both themes). "Deets" inherits
// --text-primary so it reads white on dark and near-black on light
// without a per-theme asset swap.
//
// The component intentionally has no explicit size — all internal
// dimensions are em-based so a parent font-size cascades down.
// Existing call sites already set font-size on `.logo` etc., so the
// lockup grows/shrinks with whatever container it lands in.

function Wordmark({ className = '' }) {
  return (
    <span className={`wordmark ${className}`.trim()}>
      <img src={iconMark} alt="" className="wordmark__icon" aria-hidden="true" />
      <span className="wordmark__text">
        <span className="wordmark__accent">sport</span>
        <span className="wordmark__base">Deets</span>
      </span>
    </span>
  );
}

export default Wordmark;
