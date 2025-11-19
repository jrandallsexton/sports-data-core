import React, { useEffect, useRef } from 'react';
import mermaid from 'mermaid';

const MermaidDiagram = ({ chart }) => {
  const ref = useRef(null);

  useEffect(() => {
    if (ref.current) {
      // Initialize mermaid with dark theme
      mermaid.initialize({
        startOnLoad: false,
        theme: 'dark',
        themeVariables: {
          primaryColor: '#1e40af',
          primaryTextColor: '#fff',
          primaryBorderColor: '#3b82f6',
          lineColor: '#60a5fa',
          secondaryColor: '#1e40af',
          tertiaryColor: '#1e293b',
          background: 'transparent',
          mainBkg: '#1e40af',
          secondBkg: '#7c3aed',
          tertiaryBkg: '#059669'
        }
      });

      // Render the diagram
      const renderDiagram = async () => {
        try {
          const id = `mermaid-${Math.random().toString(36).substr(2, 9)}`;
          const { svg } = await mermaid.render(id, chart);
          if (ref.current) {
            ref.current.innerHTML = svg;
          }
        } catch (error) {
          console.error('Mermaid render error:', error);
          if (ref.current) {
            ref.current.innerHTML = `<div style="color: #dc2626;">Error rendering diagram</div>`;
          }
        }
      };

      renderDiagram();
    }
  }, [chart]);

  return <div ref={ref} className="mermaid-container" />;
};

export default MermaidDiagram;
