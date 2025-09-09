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

function PickAccuracyWidget({ data, selectedGroup, onGroupChange, groups }) {
  // Find the maximum value across all datasets
  const maxValue = Math.max(
    ...Object.values(groups).flatMap(group => 
      group.data.map(d => d.accuracy)
    )
  );

  // Calculate mean values for the selected group
  const selectedGroupData = groups[selectedGroup].data;
  const pickMean = selectedGroupData.reduce((sum, item) => sum + item.accuracy, 0) / selectedGroupData.length;

  return (
    <div className="chart-block">
  <h2>Pick Accuracy by Week</h2>
      <em>(simulated until after Week 1)</em>
      <div className="group-selector">
        <select 
          value={selectedGroup} 
          onChange={(e) => onGroupChange(e.target.value)}
          className="group-dropdown"
        >
          {Object.entries(groups).map(([key, group]) => (
            <option key={key} value={key}>{group.name}</option>
          ))}
        </select>
      </div>
      <div className="chart-container">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart
            data={selectedGroupData}
            margin={{ top: 20, right: 30, left: 30, bottom: 5 }}
          >
            <defs>
              <linearGradient id="accuracyGradient" x1="0" y1="0" x2="0" y2="1">
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
              y={pickMean} 
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
                  Mean Accuracy: {pickMean.toFixed(1)}%
                </div>
              )}
            />
            <Bar 
              dataKey="accuracy" 
              fill="url(#accuracyGradient)"
              radius={[4, 4, 0, 0]}
              barSize={30}
            >
              <LabelList 
                dataKey="accuracy" 
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

export default PickAccuracyWidget;
