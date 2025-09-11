import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  ReferenceLine,
  Legend,
  LabelList,
} from "recharts";

function AiAccuracyWidget({ syntheticDto }) {
  const weekly = syntheticDto?.weeklyAccuracy || [];
  const maxValue = weekly.length > 0 ? Math.max(...weekly.map(d => d.accuracyPercent)) : 100;
  const aiMean = weekly.length > 0 ? weekly.reduce((sum, item) => sum + item.accuracyPercent, 0) / weekly.length : 0;

  return (
    <div className="chart-block">
      <h2>AI Accuracy by Week</h2>
  <div className="group-selector" style={{ minHeight: 40 }}></div>
  <div className="chart-container">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart
            data={weekly.map(w => ({
              week: w.week,
              aiAccuracy: w.accuracyPercent,
              correctPicks: w.correctPicks,
              totalPicks: w.totalPicks
            }))}
            margin={{ top: 20, right: 30, left: 30, bottom: 5 }}
          >
            <defs>
              <linearGradient id="aiAccuracyGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#00ff00" />
                <stop offset="50%" stopColor="#ffff00" />
                <stop offset="100%" stopColor="#ff0000" />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#444" />
            <XAxis 
              dataKey="week" 
              stroke="#ccc"
              tick={{ fill: '#ccc' }}
            />
            <YAxis 
              stroke="#ccc"
              tick={{ fill: '#ccc' }}
              domain={[0, maxValue]}
              tickFormatter={(value) => `${value}%`}
              width={40}
              ticks={[0, 20, 40, 60, 80, 100]}
            />
            <Tooltip 
              contentStyle={{ 
                backgroundColor: '#1a1a1a',
                border: '1px solid #333',
                borderRadius: '8px',
                color: '#ddd'
              }}
              wrapperStyle={{
                backgroundColor: 'transparent',
                border: 'none'
              }}
              cursor={{ fill: 'rgba(255, 255, 255, 0.1)' }}
              formatter={(value, name, props) => [`${props.payload.correctPicks}/${props.payload.totalPicks}`, 'Correct Picks']}
            />
            <ReferenceLine 
              y={aiMean} 
              stroke="#61dafb"
              strokeDasharray="3 3"
            />
            <Legend 
              content={() => (
                <div style={{ 
                  display: 'flex', 
                  justifyContent: 'center', 
                  marginTop: '10px',
                  color: '#61dafb'
                }}>
                  Mean Accuracy: {aiMean.toFixed(1)}%
                </div>
              )}
            />
            <Bar 
              dataKey="aiAccuracy" 
              fill="url(#aiAccuracyGradient)"
              radius={[4, 4, 0, 0]}
              barSize={30}
            >
              <LabelList 
                dataKey="aiAccuracy" 
                position="top" 
                fill="#fff"
                formatter={(value) => `${value}%`}
              />
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

export default AiAccuracyWidget;
