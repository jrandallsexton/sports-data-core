import React, { useEffect, useState } from 'react';
import './AdminPage.css';
import './AdminWidgetsPage.css';
import AdminHeader from './AdminHeader';
import apiWrapper from '../../api/apiWrapper';
import AiAccuracyWidget from '../widgets/AiAccuracyWidget';
import AiRecordWidget from '../widgets/AiRecordWidget';
import LeaderboardWidget from '../widgets/LeaderboardWidget';
import NewsWidget from '../widgets/NewsWidget';
import PickAccuracyWidget from '../widgets/PickAccuracyWidget';
import PickRecordWidget from '../widgets/PickRecordWidget';
import TipWeekWidget from '../widgets/TipWeekWidget';

/**
 * Widget gallery — every unmounted widget in components/widgets stood up
 * against real data so it can be reviewed and then promoted, fixed, or
 * deleted. See docs/features/admin-widget-gallery.md.
 *
 * RankingsWidget is skipped (it IS the rankings page) and CFPBracket is
 * skipped until CFP rankings publish in November.
 *
 * Each widget renders inside its own error boundary: a widget that throws
 * on this season's data shows its error in place and the rest of the
 * gallery still renders — that is the review, not a failure of the page.
 */

const STATUS_LABELS = {
  candidate: 'candidate',
  keep: 'keep',
  delete: 'delete',
  merge: 'merge',
};

class WidgetErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidCatch(error) {
    // Surfaced in the frame; logged so it also lands in the console for
    // a stack trace.
    console.error(`[widget gallery] ${this.props.name} threw:`, error);
  }

  render() {
    if (this.state.error) {
      return (
        <div className="widget-frame__error" role="alert">
          <strong>{this.props.name} threw while rendering.</strong>
          <pre>{String(this.state.error?.message ?? this.state.error)}</pre>
        </div>
      );
    }
    return this.props.children;
  }
}

function WidgetFrame({ entry, children }) {
  const [fullWidth, setFullWidth] = useState(false);
  return (
    <section className={`widget-frame${fullWidth ? ' widget-frame--full' : ''}`}>
      <header className="widget-frame__header">
        <div>
          <h3 className="widget-frame__title">{entry.title}</h3>
          <div className="widget-frame__meta">
            <code>{entry.source}</code>
            {entry.endpoints.length > 0 && (
              <span> · {entry.endpoints.join(', ')}</span>
            )}
          </div>
        </div>
        <div className="widget-frame__controls">
          <span className={`widget-frame__status widget-frame__status--${entry.status}`}>
            {STATUS_LABELS[entry.status]}
            {entry.mergeInto ? ` → ${entry.mergeInto}` : ''}
          </span>
          <button
            type="button"
            className="model-lab-btn model-lab-btn--small"
            onClick={() => setFullWidth((v) => !v)}
            aria-pressed={fullWidth}
          >
            {fullWidth ? 'Card width' : 'Full width'}
          </button>
        </div>
      </header>
      {entry.note && <p className="widget-frame__note">{entry.note}</p>}
      <div className="widget-frame__body">
        <WidgetErrorBoundary name={entry.title}>{children}</WidgetErrorBoundary>
      </div>
    </section>
  );
}

/**
 * Props the home page would supply to the two chart widgets. Loaded once
 * for the page; a failure shows in that widget's frame as "no data" rather
 * than blocking the others.
 */
function useChartDtos() {
  const [synthetic, setSynthetic] = useState({ data: null, error: null, loading: true });
  const [leagues, setLeagues] = useState({ data: null, error: null, loading: true });

  useEffect(() => {
    let cancelled = false;
    apiWrapper.Picks.getAccuracyChartForSynthetic()
      .then((r) => { if (!cancelled) setSynthetic({ data: r?.data ?? r, error: null, loading: false }); })
      .catch((e) => { if (!cancelled) setSynthetic({ data: null, error: e, loading: false }); });
    apiWrapper.Picks.getAccuracyChartForUser()
      .then((r) => { if (!cancelled) setLeagues({ data: r?.data ?? r, error: null, loading: false }); })
      .catch((e) => { if (!cancelled) setLeagues({ data: null, error: e, loading: false }); });
    return () => { cancelled = true; };
  }, []);

  return { synthetic, leagues };
}

function FetchState({ state, children }) {
  if (state.loading) return <div className="widget-frame__fetch">Loading…</div>;
  if (state.error) {
    return (
      <div className="widget-frame__error" role="alert">
        <strong>Data fetch failed.</strong>
        <pre>{String(state.error?.message ?? state.error)}</pre>
      </div>
    );
  }
  return children;
}

export default function AdminWidgetsPage() {
  const { synthetic, leagues } = useChartDtos();

  // The registry. status/mergeInto/note are the operator's review notes -
  // labels only, nothing reads them.
  const entries = [
    {
      key: 'ai-accuracy',
      title: 'AI Accuracy (chart)',
      source: 'components/widgets/AiAccuracyWidget.jsx',
      endpoints: ['GET /ui/picks/chart/synthetic'],
      status: 'candidate',
      note: 'First promotion target for this season.',
      render: () => (
        <FetchState state={synthetic}>
          <AiAccuracyWidget syntheticDto={synthetic.data} />
        </FetchState>
      ),
    },
    {
      key: 'ai-record',
      title: 'AI Record (card)',
      source: 'components/widgets/AiRecordWidget.jsx',
      endpoints: ['GET /ui/picks/{season}/widget/synthetic'],
      status: 'candidate',
      render: () => <AiRecordWidget />,
    },
    {
      key: 'pick-accuracy',
      title: 'Pick Accuracy (chart)',
      source: 'components/widgets/PickAccuracyWidget.jsx',
      endpoints: ['GET /ui/picks/chart'],
      status: 'candidate',
      note: "Shows the signed-in admin's own leagues.",
      render: () => (
        <FetchState state={leagues}>
          <PickAccuracyWidget leagues={leagues.data} />
        </FetchState>
      ),
    },
    {
      key: 'pick-record',
      title: 'Pick Record (card)',
      source: 'components/widgets/PickRecordWidget.jsx',
      endpoints: ['GET /ui/picks/{season}/widget'],
      status: 'candidate',
      render: () => <PickRecordWidget />,
    },
    {
      key: 'leaderboard',
      title: 'Leaderboard',
      source: 'components/widgets/LeaderboardWidget.jsx',
      endpoints: ['GET /ui/leaderboard/widget'],
      status: 'candidate',
      render: () => <LeaderboardWidget />,
    },
    {
      key: 'news',
      title: 'News',
      source: 'components/widgets/NewsWidget.jsx',
      endpoints: ['GET /ui/articles', 'GET /ui/articles/{id}'],
      status: 'candidate',
      note: 'Overlaps home/FeaturedArticleCard and home/SystemNews.',
      render: () => <NewsWidget />,
    },
    {
      key: 'tip-week',
      title: 'Tip of the Week',
      source: 'components/widgets/TipWeekWidget.jsx',
      endpoints: [],
      status: 'candidate',
      note: 'Static text; its own title says "(simulated)".',
      render: () => <TipWeekWidget />,
    },
  ];

  return (
    <div className="admin-page">
      <AdminHeader />
      <div className="admin-main">
        <h2>Widget gallery</h2>
        <p className="admin-subtitle">
          Unmounted widgets from <code>components/widgets</code>, stood up against real data for review.
          RankingsWidget (the rankings page) and CFPBracket (November) are not listed.
        </p>
        <div className="widget-gallery">
          {entries.map((entry) => (
            <WidgetFrame key={entry.key} entry={entry}>
              {entry.render()}
            </WidgetFrame>
          ))}
        </div>
      </div>
    </div>
  );
}
