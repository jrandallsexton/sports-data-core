import React, { useEffect, useRef, useState, useCallback } from 'react';
import mermaid from 'mermaid';

/*
 * COLOR STYLES - Add these back to diagram definitions to restore colors:
 * 
 * Production Overview:
 *   style Azure fill:#0ea5e9,stroke:#0284c7,stroke-width:3px
 *   style Cluster fill:#ec4899,stroke:#db2777,stroke-width:3px
 *   style Data fill:#f59e0b,stroke:#d97706,stroke-width:3px
 *   style Obs fill:#8b5cf6,stroke:#7c3aed,stroke-width:3px
 *   style Browser fill:#94a3b8
 *   style Services fill:#10b981
 * 
 * Production Azure:
 *   style FD fill:#0ea5e9
 *   style APIM fill:#8b5cf6
 *   style UI1 fill:#1e40af
 *   style UI2 fill:#0891b2
 *   style UI3 fill:#059669
 *   style AppConfig fill:#0ea5e9
 *   style KeyVault fill:#8b5cf6
 *   style Cosmos fill:#10b981
 *   style ServiceBus fill:#ec4899
 *   style ACR fill:#0284c7
 * 
 * Production Cluster:
 *   style Traefik fill:#ec4899
 *   style API fill:#10b981
 *   style Producer fill:#7c3aed
 *   style Provider fill:#dc2626
 *   style Flux fill:#14b8a6
 *   style APIM fill:#8b5cf6,stroke-dasharray: 5 5
 *   style GitHub fill:#6366f1
 *   style ACR fill:#0284c7
 * 
 * Production Data:
 *   style ESPN fill:#64748b
 *   style Provider fill:#dc2626
 *   style Cosmos fill:#10b981
 *   style Bus fill:#ec4899
 *   style Producer fill:#7c3aed
 *   style PG fill:#f59e0b
 *   style API fill:#10b981
 * 
 * Production Observability:
 *   style Prometheus fill:#ef4444
 *   style Grafana fill:#f97316
 *   style Loki fill:#a855f7
 *   style Tempo fill:#06b6d4
 *   style Seq fill:#3b82f6
 *   style API fill:#10b981,stroke-dasharray: 5 5
 *   style Producer fill:#7c3aed,stroke-dasharray: 5 5
 *   style Provider fill:#dc2626,stroke-dasharray: 5 5
 *   style Traefik fill:#ec4899,stroke-dasharray: 5 5
 * 
 * Production Complete:
 *   style UI fill:#1e40af
 *   style FrontDoor fill:#0ea5e9
 *   style APIM fill:#06b6d4
 *   style Firebase fill:#f97316
 *   style AppConfig fill:#0ea5e9
 *   style KeyVault fill:#8b5cf6
 *   style ServiceBus fill:#ec4899
 *   style Cosmos fill:#10b981
 *   style ACR fill:#6366f1
 *   style Traefik fill:#059669
 *   style API fill:#059669
 *   style Producer fill:#7c3aed
 *   style Provider fill:#dc2626
 *   style PG fill:#f59e0b
 *   style Flux fill:#3b82f6
 *   style Prometheus fill:#e74c3c
 *   style Grafana fill:#f39c12
 *   style Loki fill:#3498db
 *   style Tempo fill:#9b59b6
 *   style Seq fill:#1abc9c
 *   style GitHub fill:#64748b
 *   style ESPN fill:#64748b
 * 
 * Dev Overview:
 *   style Azure fill:#0ea5e9,stroke:#0284c7,stroke-width:3px
 *   style AppSvc fill:#10b981,stroke:#059669,stroke-width:3px
 *   style Data fill:#f59e0b,stroke:#d97706,stroke-width:3px
 *   style Browser fill:#94a3b8
 * 
 * Dev Azure:
 *   style UI fill:#1e40af
 *   style Firebase fill:#f97316
 *   style AppConfig fill:#0ea5e9
 *   style KeyVault fill:#8b5cf6
 *   style Cosmos fill:#10b981
 *   style ServiceBus fill:#ec4899
 *   style Blob fill:#6366f1
 * 
 * Dev Cluster:
 *   style API fill:#059669
 *   style Producer fill:#7c3aed
 *   style Provider fill:#dc2626
 *   style Seq fill:#3b82f6
 *   style UI fill:#1e40af
 *   style Bus fill:#ec4899
 *   style ESPN fill:#64748b
 *   style AppConfig fill:#0ea5e9,stroke-dasharray: 5 5
 *   style KeyVault fill:#8b5cf6,stroke-dasharray: 5 5
 * 
 * Dev Data:
 *   style PGVM fill:#f59e0b
 *   style VNet fill:#0ea5e9
 *   style Cosmos fill:#10b981
 *   style Blob fill:#6366f1
 *   style API fill:#059669
 *   style Producer fill:#7c3aed
 *   style Provider fill:#dc2626
 */

// Azure icon paths (unused - keeping for future reference)
// const icons = {
//   frontDoor: '/azure-icons/networking/10073-icon-service-Front-Door-and-CDN-Profiles.svg',
//   apim: '/azure-icons/integration/10042-icon-service-API-Management-Services.svg',
//   cosmosDb: '/azure-icons/databases/10121-icon-service-Azure-Cosmos-DB.svg',
//   appConfig: '/azure-icons/integration/10219-icon-service-App-Configuration.svg',
//   keyVault: '/azure-icons/security/10245-icon-service-Key-Vaults.svg',
//   serviceBus: '/azure-icons/integration/10836-icon-service-Azure-Service-Bus.svg',
//   containerRegistry: '/azure-icons/containers/10105-icon-service-Container-Registries.svg',
// };

// Helper function to create icon + text label (unused - keeping for future reference)
// const icon = (path, text, size = 20) => `<img src='${path}' width='${size}'/> ${text}`;

// Initialize Mermaid
mermaid.initialize({
  startOnLoad: false,
  theme: 'dark',
  securityLevel: 'loose', // Required to render HTML in labels
  themeVariables: {
    // GitHub-style monochrome (currently active)
    primaryColor: '#333',
    primaryTextColor: '#ccc',
    primaryBorderColor: '#ccc',
    lineColor: 'lightgrey',
    secondaryColor: '#444',
    tertiaryColor: '#555',
    background: '#1f2937',
    mainBkg: '#1f2020',
    secondBkg: '#2d3748',
    border1: '#6b7280',
    border2: '#9ca3af',
    // Uncomment below and comment above for colorful version:
    /*
    primaryColor: '#1e40af',
    primaryTextColor: '#fff',
    primaryBorderColor: '#3b82f6',
    lineColor: '#60a5fa',
    secondaryColor: '#7c3aed',
    tertiaryColor: '#059669',
    background: '#1f2937',
    mainBkg: '#374151',
    secondBkg: '#4b5563',
    border1: '#6b7280',
    border2: '#9ca3af',
    */
  },
  flowchart: {
    curve: 'basis',
    padding: 20,
  },
});

// Static diagram configurations
const diagrams = {
  prod: {
    overview: {
      title: 'Production Architecture Overview',
      description: 'Click on any component to see detailed view',
      diagram: `
graph LR
    Users[👤 Users] -->|HTTPS| Azure[☁️ Azure Services]
    Azure -->|HTTP| Cluster[☸️ K3s Cluster]
    Cluster --> Services[⚙️ App Services]
    Services -->|Store/Query| Data[💾 Data Layer]
    Services -.->|Metrics/Logs| Obs[📊 Observability]
    Azure -.->|Config| Services`
    },
      azure: {
        title: 'Azure Services',
        description: 'Front Door, API Management, Static Web Apps, and supporting services',
        diagram: `
graph TB
    FD[Front Door<br/>CDN + WAF + SSL]
    APIM[API Management<br/>Rate Limit + Swagger]
    UI[Static Web App<br/>sportdeets.com]
    SignalRSvc[SignalR Service<br/>WebSocket Hub]
    
    subgraph "Configuration"
        AppConfig[App Configuration]
        KeyVault[Key Vault]
    end
    
    subgraph "Data & Messaging"
        Cosmos[(Cosmos DB<br/>Raw JSON)]
        ServiceBus[Service Bus<br/>Job Queues]
        ACR[Container Registry]
    end
    
    FD --> UI
    FD --> APIM
    FD --> SignalRSvc
    AppConfig -.->|Secrets| KeyVault`
      },
      cluster: {
        title: 'K3s Cluster',
        description: 'Traefik ingress, application pods, and GitOps deployment',
        diagram: `
graph TB
    APIM[API Management] -->|HTTP| Traefik
    
    Traefik[Traefik Ingress<br/>L7 Router]
    
    subgraph "Application Pods"
        API[API Service:8080<br/>SignalR Hub<br/>Hangfire Dashboard]
        Producer[Producer:80<br/>Transform<br/>Hangfire Jobs]
        Provider[Provider:80<br/>ESPN Fetch<br/>Hangfire Jobs]
    end
    
    Flux[Flux CD<br/>GitOps]
    
    Traefik --> API & Producer & Provider
    GitHub[GitHub] -->|Watch| Flux
    ACR[Container Registry] -->|Pull Images| Flux
    Flux -.->|Deploy| API & Producer & Provider`
      },
      data: {
        title: 'Data Layer',
        description: 'Data flow from external APIs through Cosmos DB and PostgreSQL',
        diagram: `
graph LR
    ESPN[ESPN API] -->|Game Data| Provider
    YouTube[YouTube API] -->|Videos| Provider
    
    Provider[Provider] -->|Store Raw| Cosmos[(Cosmos DB<br/>Azure)]
    Provider -->|Enqueue| Bus[Service Bus]
    
    Bus -->|Trigger| Producer[Producer]
    Producer -->|Read Raw| Cosmos
    Producer -->|Transform & Store| PG[(PostgreSQL<br/>On-Prem)]
    
    Ollama[Ollama LLM<br/>On-Prem via Ngrok] -.->|AI Analysis| Producer
    
    API[API] -->|Query| PG
    API -->|Email| SendGrid[SendGrid]
    API -->|SMS| Twilio[Twilio]`
      },
      observability: {
        title: 'Observability Stack',
        description: 'Metrics, logs, and traces collection and visualization',
        diagram: `
graph TB
    subgraph "Data Sources"
        API[API Service<br/>+ Hangfire]
        Producer[Producer<br/>+ Hangfire]
        Provider[Provider<br/>+ Hangfire]
        Traefik[Traefik]
    end
    
    subgraph "Collection"
        Prometheus[Prometheus<br/>Metrics]
        Loki[Loki<br/>Logs]
        Tempo[Tempo<br/>Traces]
        Seq[Seq<br/>Structured Logs]
    end
    
    Grafana[Grafana<br/>Dashboard]
    Hangfire[Hangfire<br/>Job Dashboards]
    
    API & Producer & Provider -.->|Logs| Seq
    API & Producer & Provider -.->|Logs| Loki
    API & Producer & Provider -.->|Traces| Tempo
    API & Producer & Provider -.->|Job Status| Hangfire
    Traefik -->|Metrics| Prometheus
    
    Prometheus & Loki & Tempo --> Grafana`
      },
      complete: {
        title: 'Complete Production Architecture',
        description: 'Full end-to-end system view',
        diagram: `
graph LR
    Users[👤 Users]
    GitHub[GitHub Repo]
    
    subgraph Azure["Azure Cloud"]
        direction TB
        UI[React UI<br/>sportdeets.com]
        FrontDoor["<img src='/azure-icons/networking/10073-icon-service-Front-Door-and-CDN-Profiles.svg' width='24'/><br/>Azure Front Door"]
        APIM["<img src='/azure-icons/integration/10042-icon-service-API-Management-Services.svg' width='24'/><br/>API Management"]
        SignalRSvc[SignalR Service<br/>WebSocket Hub]
        Firebase[Firebase Auth]
        AppConfig["<img src='/azure-icons/integration/10219-icon-service-App-Configuration.svg' width='24'/><br/>App Configuration"]
        KeyVault["<img src='/azure-icons/security/10245-icon-service-Key-Vaults.svg' width='24'/><br/>Key Vault"]
        ServiceBus["<img src='/azure-icons/integration/10836-icon-service-Azure-Service-Bus.svg' width='24'/><br/>Service Bus"]
        Cosmos["<img src='/azure-icons/databases/10121-icon-service-Azure-Cosmos-DB.svg' width='24'/><br/>Cosmos DB"]
        ACR["<img src='/azure-icons/containers/10105-icon-service-Container-Registries.svg' width='24'/><br/>Container Registry"]
    end
    
    ESPN[ESPN API]
    YouTube[YouTube API]
    
    subgraph Cluster["Homelab K3s Cluster"]
        direction TB
        Traefik[Traefik Ingress]
        API[API Service<br/>SignalR + Hangfire]
        Producer[Producer<br/>+ Hangfire]
        Provider[Provider<br/>+ Hangfire]
        Flux[Flux CD]
        Prometheus[Prometheus]
        Grafana[Grafana]
        Loki[Loki]
        Tempo[Tempo]
        Seq[Seq]
        Hangfire[Hangfire Dashboards]
        ClusterSpacer[ ]
    end
    
    subgraph OnPrem["On-Prem Data"]
        direction TB
        PG[(PostgreSQL<br/>Server)]
        Ollama[Ollama LLM<br/>via Ngrok]
    end
    
    subgraph External["External Services"]
        direction TB
        SendGrid[SendGrid<br/>Email]
        Twilio[Twilio<br/>SMS]
    end
    
    Users -->|HTTPS| FrontDoor
    Users -->|WebSocket| FrontDoor
    FrontDoor -->|Route| APIM
    FrontDoor -->|Route| SignalRSvc
    APIM -->|Proxy| Traefik
    Traefik --> API
    FrontDoor -.->|CDN| UI
    SignalRSvc <-->|Connect| API
    
    UI <-->|Auth| Firebase
    UI <-->|Real-time| SignalRSvc
    
    GitHub -->|GitOps| Flux
    Flux -.->|Deploy| API
    Flux -.->|Deploy| Producer
    Flux -.->|Deploy| Provider
    ACR -->|Pull Images| API
    ACR -->|Pull Images| Producer
    ACR -->|Pull Images| Provider
    
    ESPN -->|Game Data| Provider
    YouTube -->|Videos| Provider
    Provider -->|Raw JSON| Cosmos
    Provider -->|Enqueue| ServiceBus
    ServiceBus -->|Trigger| Producer
    
    AppConfig -.->|Settings| API
    AppConfig -.->|Settings| Producer
    AppConfig -.->|Settings| Provider
    KeyVault -.->|Secrets| AppConfig
    
    API -.->|Metrics| Prometheus
    Producer -.->|Metrics| Prometheus
    Provider -.->|Metrics| Prometheus
    API -.->|Logs| Loki
    Producer -.->|Logs| Loki
    Provider -.->|Logs| Loki
    API -.->|Traces| Tempo
    Producer -.->|Traces| Tempo
    Provider -.->|Traces| Tempo
    API -.->|Structured| Seq
    Producer -.->|Structured| Seq
    Provider -.->|Structured| Seq
    Prometheus -->|Visualize| Grafana
    Loki -->|Visualize| Grafana
    Tempo -->|Visualize| Grafana
    
    ClusterSpacer -.-> PG
    ClusterSpacer -.-> SendGrid
    Producer -->|Read| Cosmos
    Producer -->|Transform| PG
    Producer -.->|AI Analysis| Ollama
    API -->|Query| PG
    API -->|Notifications| SendGrid
    API -->|Notifications| Twilio
    
    style ClusterSpacer fill:none,stroke:none`
      }
    },
    dev: {
      overview: {
        title: 'Development Architecture Overview',
        description: 'Click on any component to see detailed view',
        diagram: `
graph LR
    Browser[👤 Users] -->|HTTPS| Azure[☁️ Azure Cloud]
    Azure --> AppSvc[⚙️ App Service Plan]
    AppSvc -->|VNet| Data[💾 Data Layer]`
      },
      azure: {
        title: 'Azure Services',
        description: 'UI, authentication, and configuration services',
        diagram: `
graph TB
    UI[React UI<br/>dev.sportdeets.com]
    Firebase[Firebase Auth]
    
    subgraph "Configuration"
        AppConfig[App Configuration]
        KeyVault[Key Vault]
    end
    
    subgraph "Data & Messaging"
        Cosmos[(Cosmos DB<br/>Raw JSON)]
        ServiceBus[Service Bus]
        Blob[Blob Storage]
    end
    
    UI -.->|Authentication| Firebase
    AppConfig -.->|Secrets| KeyVault`
      },
      cluster: {
        title: 'App Service Plan',
        description: 'Single App Service Plan with 4 apps connected via VNet',
        diagram: `
graph TB
    subgraph "App Service Plan"
        API[API Service<br/>SignalR + Hangfire]
        Producer[Producer<br/>+ Hangfire]
        Provider[Provider<br/>+ Hangfire]
        Seq[Seq<br/>Structured Logs]
    end
    
    UI[React UI] <-->|WebSocket| API
    UI -->|HTTPS| API
    
    ESPN[ESPN API] -->|Fetch| Provider
    Provider -->|Enqueue| Bus[Service Bus]
    Bus -->|Trigger| Producer
    
    API & Producer & Provider -.->|Logs| Seq
    
    AppConfig[App Config] -.->|Settings| API & Producer & Provider
    KeyVault[Key Vault] -.->|Secrets| API & Producer & Provider`
      },
      data: {
        title: 'Data Layer',
        description: 'VNet-connected PostgreSQL VM, Cosmos DB, and Blob Storage',
        diagram: `
graph TB
    subgraph "App Service Plan"
        API[API]
        Producer[Producer]
        Provider[Provider]
    end
    
    subgraph "VNet Integration"
        VNet[Azure VNet]
    end
    
    subgraph "Data Services"
        PGVM[(PostgreSQL<br/>Azure VM)]
        Cosmos[(Cosmos DB<br/>Raw JSON)]
        Blob[Blob Storage]
    end
    
    subgraph "External APIs"
        ESPN[ESPN API]
        YouTube[YouTube API]
    end
    
    subgraph "On-Prem"
        Ollama[Ollama LLM<br/>via Ngrok]
    end
    
    subgraph "Notifications"
        SendGrid[SendGrid<br/>Email]
        Twilio[Twilio<br/>SMS]
    end
    
    API & Producer & Provider -->|VNet| VNet
    VNet -->|Private| PGVM
    
    ESPN -->|Game Data| Provider
    YouTube -->|Videos| Provider
    Provider -->|Store Raw| Cosmos
    Producer -->|Read Raw| Cosmos
    Producer -->|Transform| PGVM
    Producer -->|Upload Logos| Blob
    Producer -.->|AI Analysis| Ollama
    
    API -->|Query| PGVM
    API -->|Read Logos| Blob
    API -->|Notifications| SendGrid
    API -->|Notifications| Twilio`
      },
      complete: {
        title: 'Complete Development Architecture',
        description: 'Full end-to-end development system view',
        diagram: `
graph TB
    subgraph "Azure Cloud"
        UI[React UI<br/>dev.sportdeets.com]
        Firebase[Firebase Auth]
        AppConfig["<img src='/azure-icons/integration/10219-icon-service-App-Configuration.svg' width='24'/><br/>App Configuration"]
        KeyVault["<img src='/azure-icons/security/10245-icon-service-Key-Vaults.svg' width='24'/><br/>Key Vault"]
        ServiceBus["<img src='/azure-icons/integration/10836-icon-service-Azure-Service-Bus.svg' width='24'/><br/>Service Bus"]
        Cosmos["<img src='/azure-icons/databases/10121-icon-service-Azure-Cosmos-DB.svg' width='24'/><br/>Cosmos DB"]
        ACR["<img src='/azure-icons/containers/10105-icon-service-Container-Registries.svg' width='24'/><br/>Container Registry"]
        Blob[Blob Storage]
        
        subgraph "App Service Plan"
            API[API Service<br/>SignalR + Hangfire]
            Producer[Producer<br/>+ Hangfire]
            Provider[Provider<br/>+ Hangfire]
            Seq[Seq<br/>Structured Logs]
        end
        
        subgraph "VNet"
            PGVM[(PostgreSQL<br/>Azure VM)]
        end
    end
    
    subgraph "On-Prem"
        Ollama[Ollama LLM<br/>via Ngrok]
    end
    
    subgraph "External Services"
        SendGrid[SendGrid<br/>Email]
        Twilio[Twilio<br/>SMS]
    end
    
    GitHub[GitHub Repo]
    ESPN[ESPN API]
    YouTube[YouTube API]
    
    UI <-->|Auth| Firebase
    UI <-->|Real-time| API
    
    ESPN -->|Game Data| Provider
    YouTube -->|Videos| Provider
    Provider -->|Raw JSON| Cosmos
    Provider -->|Enqueue| ServiceBus
    ServiceBus -->|Trigger| Producer
    Producer -->|Read| Cosmos
    Producer -->|Transform| PGVM
    Producer -->|Upload Logos| Blob
    Producer -.->|AI Analysis| Ollama
    
    API -->|Query| PGVM
    API -->|Read Logos| Blob
    API -->|Notifications| SendGrid
    API -->|Notifications| Twilio
    
    API & Producer & Provider -.->|Logs| Seq
    
    AppConfig -.->|Settings| API & Producer & Provider
    KeyVault -.->|Secrets| AppConfig
    
    ACR -->|Pull Images| API & Producer & Provider`
    }
  }
};

const ArchitectureDiagrams = () => {
  const [activeEnv, setActiveEnv] = useState('prod');
  const [activeView, setActiveView] = useState('overview');
  const [isZoomed, setIsZoomed] = useState(false);
  const diagramRef = useRef(null);
  const zoomedDiagramRef = useRef(null);

  const getActiveDiagram = useCallback(() => {
    return diagrams[activeEnv][activeView] || diagrams[activeEnv].overview;
  }, [activeEnv, activeView]); // diagrams is now a module constant, no need to include in deps

  const viewOptions = {
    prod: [
      { id: 'overview', label: 'Overview', icon: '🏠' },
      { id: 'azure', label: 'Azure', icon: '☁️' },
      { id: 'cluster', label: 'K3s Cluster', icon: '☸️' },
      { id: 'data', label: 'Data', icon: '💾' },
      { id: 'observability', label: 'Observability', icon: '📊' },
      { id: 'complete', label: 'Complete View', icon: '🗺️' }
    ],
    dev: [
      { id: 'overview', label: 'Overview', icon: '🏠' },
      { id: 'azure', label: 'Azure', icon: '☁️' },
      { id: 'cluster', label: 'App Service Plan', icon: '📦' },
      { id: 'data', label: 'Data + VNet', icon: '💾' },
      { id: 'complete', label: 'Complete View', icon: '🗺️' }
    ]
  };

  useEffect(() => {
    const renderDiagram = async () => {
      if (!diagramRef.current) return;
      
      try {
        const activeDiagram = getActiveDiagram();
        const diagramId = `diagram-${activeEnv}-${activeView}-${Date.now()}`;
        const { svg } = await mermaid.render(diagramId, activeDiagram.diagram);
        diagramRef.current.innerHTML = svg;
        
        // Set width to 100% like GitHub does
        const svgElement = diagramRef.current.querySelector('svg');
        if (svgElement) {
          svgElement.setAttribute('width', '100%');
          svgElement.setAttribute('height', 'auto');
          svgElement.style.maxWidth = '100%';
        }
      } catch (error) {
        console.error('Mermaid rendering error:', error);
        diagramRef.current.innerHTML = `<div style="color: #ef4444; padding: 2rem;">Error rendering diagram: ${error.message}</div>`;
      }
    };

    renderDiagram();
  }, [activeEnv, activeView, getActiveDiagram]);

  useEffect(() => {
    const renderZoomedDiagram = async () => {
      if (!isZoomed || !zoomedDiagramRef.current) return;
      
      try {
        const activeDiagram = getActiveDiagram();
        const diagramId = `zoomed-diagram-${activeEnv}-${activeView}-${Date.now()}`;
        const { svg } = await mermaid.render(diagramId, activeDiagram.diagram);
        zoomedDiagramRef.current.innerHTML = svg;
        
        // Make zoomed SVG larger and scrollable
        const svgElement = zoomedDiagramRef.current.querySelector('svg');
        if (svgElement) {
          svgElement.setAttribute('width', '100%');
          svgElement.style.minWidth = '1200px';
        }
      } catch (error) {
        console.error('Mermaid rendering error:', error);
        zoomedDiagramRef.current.innerHTML = `<div style="color: #ef4444; padding: 2rem;">Error rendering diagram: ${error.message}</div>`;
      }
    };

    renderZoomedDiagram();
  }, [isZoomed, activeEnv, activeView, getActiveDiagram]);

  const handleCloseZoom = () => {
    setIsZoomed(false);
  };

  const activeDiagram = getActiveDiagram();

  return (
    <div className="architecture-diagrams">
      <div className="diagram-controls">
        <div className="env-tabs">
          <button
            className={`env-tab ${activeEnv === 'dev' ? 'active' : ''}`}
            onClick={() => { setActiveEnv('dev'); setActiveView('overview'); }}
          >
            Development (Azure)
          </button>
          <button
            className={`env-tab ${activeEnv === 'prod' ? 'active' : ''}`}
            onClick={() => { setActiveEnv('prod'); setActiveView('overview'); }}
          >
            Production (Azure/On-Prem)
          </button>
        </div>

        <div className="view-buttons">
          {viewOptions[activeEnv].map(option => (
            <button
              key={option.id}
              className={`view-btn ${activeView === option.id ? 'active' : ''}`}
              onClick={() => setActiveView(option.id)}
              title={option.label}
            >
              <span className="view-icon">{option.icon}</span>
              <span className="view-label">{option.label.replace(/^..\s/, '')}</span>
            </button>
          ))}
        </div>
      </div>

      <div className="diagram-content">
        <div className="diagram-header">
          <h3>{activeDiagram.title}</h3>
          <p>{activeDiagram.description}</p>
        </div>
        <div style={{ position: 'relative' }}>
          <div 
            ref={diagramRef} 
            className="mermaid-diagram"
            onClick={() => setIsZoomed(true)}
            style={{ cursor: 'zoom-in' }}
          />
          <div style={{
            position: 'absolute',
            top: '1rem',
            right: '1rem',
            backgroundColor: 'rgba(31, 41, 55, 0.9)',
            padding: '0.5rem 1rem',
            borderRadius: '0.25rem',
            fontSize: '0.75rem',
            color: '#9ca3af',
            border: '1px solid #374151'
          }}>
            Click to zoom 🔍
          </div>
        </div>
      </div>

      {/* Zoom Overlay */}
      {isZoomed && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: 'rgba(0, 0, 0, 0.95)',
          zIndex: 9999,
          display: 'flex',
          flexDirection: 'column',
          padding: '1rem'
        }}>
          {/* Zoom Controls */}
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: '1rem',
            padding: '1rem',
            backgroundColor: '#1f2937',
            borderRadius: '0.5rem',
            border: '1px solid #374151'
          }}>
            <div style={{ color: '#f9fafb', fontSize: '1.125rem', fontWeight: '600' }}>
              {activeDiagram.title}
            </div>
            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <span style={{ color: '#9ca3af', fontSize: '0.875rem' }}>
                Scroll to navigate • Click and drag to pan
              </span>
              <button
                onClick={handleCloseZoom}
                style={{
                  padding: '0.5rem 1rem',
                  backgroundColor: '#ef4444',
                  color: '#fff',
                  border: 'none',
                  borderRadius: '0.25rem',
                  cursor: 'pointer',
                  fontSize: '1.25rem',
                  fontWeight: 'bold',
                  marginLeft: '1rem'
                }}
              >
                ✕
              </button>
            </div>
          </div>

          {/* Zoomed Diagram */}
          <div style={{
            flex: 1,
            overflow: 'auto',
            backgroundColor: '#111827',
            borderRadius: '0.5rem',
            padding: '2rem'
          }}>
            <div 
              ref={zoomedDiagramRef}
              style={{
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'flex-start'
              }}
            />
          </div>
        </div>
      )}

      <style jsx>{`
        .architecture-diagrams {
          margin: 2rem 0;
        }

        .diagram-controls {
          margin-bottom: 1.5rem;
        }

        .env-tabs {
          display: flex;
          gap: 1rem;
          margin-bottom: 1rem;
          border-bottom: 2px solid #374151;
        }

        .env-tab {
          padding: 0.75rem 1.5rem;
          background: transparent;
          border: none;
          border-bottom: 3px solid transparent;
          color: #9ca3af;
          font-size: 1rem;
          font-weight: 500;
          cursor: pointer;
          transition: all 0.3s ease;
        }

        .env-tab:hover {
          color: #e5e7eb;
          border-bottom-color: #4b5563;
        }

        .env-tab.active {
          color: #60a5fa;
          border-bottom-color: #3b82f6;
        }

        .view-buttons {
          display: flex;
          gap: 0.5rem;
          flex-wrap: wrap;
        }

        .view-btn {
          display: flex;
          align-items: center;
          gap: 0.5rem;
          padding: 0.5rem 1rem;
          background: #374151;
          border: 2px solid #4b5563;
          border-radius: 0.5rem;
          color: #d1d5db;
          font-size: 0.9rem;
          font-weight: 500;
          cursor: pointer;
          transition: all 0.2s ease;
        }

        .view-btn:hover {
          background: #4b5563;
          border-color: #60a5fa;
          color: #f9fafb;
        }

        .view-btn.active {
          background: #1e40af;
          border-color: #3b82f6;
          color: #fff;
        }

        .view-icon {
          font-size: 1.2rem;
        }

        .view-label {
          font-size: 0.9rem;
        }

        .diagram-content {
          background: #1f2937;
          border-radius: 0.5rem;
          padding: 1.5rem;
          border: 1px solid #374151;
        }

        .diagram-header h3 {
          margin: 0 0 0.5rem 0;
          color: #f9fafb;
          font-size: 1.5rem;
        }

        .diagram-header p {
          color: #9ca3af;
          margin: 0 0 1.5rem 0;
          font-size: 0.95rem;
        }

        .mermaid-diagram {
          overflow-x: auto;
          padding: 1rem;
          background: #111827;
          border-radius: 0.375rem;
          min-height: 400px;
          display: flex;
          align-items: center;
          justify-content: center;
        }

        .mermaid-diagram svg {
          max-width: 100%;
          height: auto;
        }

        @media (max-width: 768px) {
          .view-buttons {
            flex-direction: column;
          }

          .view-btn {
            width: 100%;
            justify-content: center;
          }
        }
      `}</style>
    </div>
  );
};

export default ArchitectureDiagrams;
