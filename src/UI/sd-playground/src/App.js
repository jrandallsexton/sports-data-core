import { Toaster } from 'react-hot-toast';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import logo from './logo.svg';
import './App.css';

// Import your real app shell
import MainApp from './MainApp';

function LandingPage() {
  return (
    <div className="App">
      <header className="App-header">
        <img src={logo} className="App-logo" alt="logo" />
        <p>sportDeets.com</p>
        <p>Coming August 2025</p>
        <Link
          to="/app"
          className="App-link"
        >
          Enter App
        </Link>
      </header>
    </div>
  );
}

function App() {
  return (
    <Router>
      <Toaster position="top-center" reverseOrder={false} />
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/app/*" element={<MainApp />} />
      </Routes>
    </Router>
  );
}

export default App;
