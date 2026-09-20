import { useState } from 'react';
import Sidebar from './components/Sidebar';
import Overview from './sections/Overview';
import Architecture from './sections/Architecture';
import SourceCode from './sections/SourceCode';
import Settings from './sections/Settings';
import HarmonyPatches from './sections/HarmonyPatches';
import Compatibility from './sections/Compatibility';
import Improvements from './sections/Improvements';
import Optimized from './sections/Optimized';
import QuickReference from './sections/QuickReference';

export type Section = 'overview' | 'architecture' | 'source' | 'settings' | 'harmony' | 'compatibility' | 'improvements' | 'optimized' | 'reference';

function App() {
  const [activeSection, setActiveSection] = useState<Section>('optimized');
  const [sidebarOpen, setSidebarOpen] = useState(true);

  const renderSection = () => {
    switch (activeSection) {
      case 'overview': return <Overview />;
      case 'architecture': return <Architecture />;
      case 'source': return <SourceCode />;
      case 'settings': return <Settings />;
      case 'harmony': return <HarmonyPatches />;
      case 'compatibility': return <Compatibility />;
      case 'improvements': return <Improvements />;
      case 'optimized': return <Optimized />;
      case 'reference': return <QuickReference />;
      default: return <Overview />;
    }
  };

  return (
    <div className="min-h-screen bg-gray-900 text-gray-100 flex">
      <Sidebar
        activeSection={activeSection}
        setActiveSection={setActiveSection}
        isOpen={sidebarOpen}
        toggle={() => setSidebarOpen(!sidebarOpen)}
      />
      <main className={`flex-1 transition-all duration-300 ${sidebarOpen ? 'ml-64' : 'ml-16'}`}>
        <div className="p-6 max-w-6xl mx-auto">
          {renderSection()}
        </div>
      </main>
    </div>
  );
}

export default App;
