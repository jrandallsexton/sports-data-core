import React, { useState, useEffect } from "react";
import apiWrapper from "../../api/apiWrapper";
import "./TeamLogos.css";

export default function TeamLogos({ slug, seasonYear, sport, league }) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState({});

  useEffect(() => {
    if (!slug) return;

    const fetchLogos = async () => {
      try {
        setLoading(true);
        const response = await apiWrapper.LogoAdmin.getFranchiseLogos(sport, league, slug, seasonYear);
        setData(response.data);
      } catch (err) {
        console.error("Failed to load logos:", err);
        setError("Failed to load logos");
      } finally {
        setLoading(false);
      }
    };

    fetchLogos();
  }, [slug, seasonYear, sport, league]);

  const handleToggle = async (logoId, currentValue, logoType) => {
    const newValue = !currentValue;
    setSaving((prev) => ({ ...prev, [logoId]: true }));

    try {
      await apiWrapper.LogoAdmin.updateLogoDarkBg(sport, league, slug, seasonYear, logoId, newValue, logoType);

      // Update local state
      setData((prev) => {
        const updated = { ...prev };

        if (logoType === "franchise") {
          updated.franchiseLogos = updated.franchiseLogos.map((l) =>
            l.id === logoId ? { ...l, isForDarkBg: newValue } : l
          );
        } else {
          updated.seasonLogos = updated.seasonLogos.map((season) => ({
            ...season,
            logos: season.logos.map((l) =>
              l.id === logoId ? { ...l, isForDarkBg: newValue } : l
            ),
          }));
        }

        return updated;
      });
    } catch (err) {
      console.error("Failed to update logo:", err);
    } finally {
      setSaving((prev) => ({ ...prev, [logoId]: false }));
    }
  };

  const renderLogo = (logo, logoType) => (
    <div key={logo.id} className="logo-admin-item">
      <div className="logo-admin-preview">
        <div className="logo-admin-preview-dark">
          <img src={logo.url} alt="Logo on dark" />
        </div>
        <div className="logo-admin-preview-light">
          <img src={logo.url} alt="Logo on light" />
        </div>
      </div>
      <div className="logo-admin-details">
        <div className="logo-admin-dimensions">
          {logo.width && logo.height ? `${logo.width}x${logo.height}` : "—"}
        </div>
        {logo.rel && logo.rel.length > 0 && (
          <div className="logo-admin-rel">
            {logo.rel.map((r, i) => (
              <span key={i} className="logo-admin-tag">{r}</span>
            ))}
          </div>
        )}
      </div>
      <div className="logo-admin-toggle">
        <label className="logo-admin-switch">
          <input
            type="checkbox"
            checked={logo.isForDarkBg === true}
            onChange={() => handleToggle(logo.id, logo.isForDarkBg, logoType)}
            disabled={saving[logo.id]}
          />
          <span className="logo-admin-slider"></span>
        </label>
        <span className="logo-admin-toggle-label">
          {logo.isForDarkBg ? "Dark BG" : "Not set"}
        </span>
      </div>
    </div>
  );

  if (loading) return <div className="logo-admin-loading">Loading logos...</div>;
  if (error) return <div className="logo-admin-error">{error}</div>;
  if (!data) return null;

  return (
    <div className="logo-admin">
      <div className="logo-admin-section">
        <h3 className="logo-admin-section-title">Franchise Logos</h3>
        <div className="logo-admin-grid">
          {data.franchiseLogos.length > 0
            ? data.franchiseLogos.map((l) => renderLogo(l, "franchise"))
            : <div className="logo-admin-empty">No franchise logos</div>}
        </div>
      </div>

      {data.seasonLogos.map((season) => (
        <div key={season.franchiseSeasonId} className="logo-admin-section">
          <h3 className="logo-admin-section-title">{season.seasonYear} Season Logos</h3>
          <div className="logo-admin-grid">
            {season.logos.length > 0
              ? season.logos.map((l) => renderLogo(l, "franchiseSeason"))
              : <div className="logo-admin-empty">No logos for this season</div>}
          </div>
        </div>
      ))}
    </div>
  );
}
