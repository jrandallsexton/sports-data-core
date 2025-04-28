import { Toaster } from 'react-hot-toast';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import './App.css';

import MainApp from './MainApp';
import SignupPage from './components/signup/SignupPage';
import LandingPage from './components/landing/LandingPage';
import TermsPage from "./components/legal/TermsPage";
import PrivacyPage from "./components/legal/PrivacyPage";
import { ThemeProvider } from "./contexts/ThemeContext";

function App() {
  return (
    <ThemeProvider>
      <Router>
        <Toaster position="top-center" reverseOrder={false} />
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/signup" element={<SignupPage />} />
          <Route path="/app/*" element={<MainApp />} />
          <Route path="/terms" element={<TermsPage />} />
          <Route path="/privacy" element={<PrivacyPage />} />
        </Routes>
      </Router>
    </ThemeProvider>
  );
}

export default App;
