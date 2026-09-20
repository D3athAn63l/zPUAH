import { Section } from '../App';

interface SidebarProps {
  activeSection: Section;
  setActiveSection: (section: Section) => void;
  isOpen: boolean;
  toggle: () => void;
}

const navItems: { id: Section; label: string; icon: string }[] = [
  { id: 'overview', label: 'Overview', icon: '📋' },
  { id: 'architecture', label: 'Architecture', icon: '🏗️' },
  { id: 'source', label: 'Source Code', icon: '💻' },
  { id: 'settings', label: 'Settings', icon: '⚙️' },
  { id: 'harmony', label: 'Harmony Patches', icon: '🔧' },
  { id: 'compatibility', label: 'Compatibility', icon: '🔗' },
  { id: 'improvements', label: 'Improvement Ideas', icon: '💡' },
  { id: 'optimized', label: 'Optimized Version', icon: '⚡' },
  { id: 'build', label: 'Build Guide', icon: '🔨' },
  { id: 'reference', label: 'Quick Reference', icon: '📖' },
];

export default function Sidebar({ activeSection, setActiveSection, isOpen, toggle }: SidebarProps) {
  return (
    <aside className={`fixed left-0 top-0 h-full bg-gray-800 border-r border-gray-700 transition-all duration-300 z-50 ${isOpen ? 'w-64' : 'w-16'}`}>
      <div className="flex items-center justify-between p-4 border-b border-gray-700">
        {isOpen && (
          <div className="flex items-center gap-2">
            <span className="text-xl">📦</span>
            <span className="font-bold text-sm text-orange-400">PUAH Toolkit</span>
          </div>
        )}
        <button
          onClick={toggle}
          className="p-1.5 rounded hover:bg-gray-700 transition-colors text-gray-400 hover:text-white"
        >
          {isOpen ? '◀' : '▶'}
        </button>
      </div>

      <nav className="mt-4 px-2">
        {navItems.map((item) => (
          <button
            key={item.id}
            onClick={() => setActiveSection(item.id)}
            className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg mb-1 transition-all text-left
              ${activeSection === item.id
                ? 'bg-orange-500/20 text-orange-400 border border-orange-500/30'
                : 'text-gray-400 hover:bg-gray-700 hover:text-white border border-transparent'
              }`}
          >
            <span className="text-lg flex-shrink-0">{item.icon}</span>
            {isOpen && <span className="text-sm font-medium truncate">{item.label}</span>}
          </button>
        ))}
      </nav>

      {isOpen && (
        <div className="absolute bottom-4 left-4 right-4">
          <div className="bg-gray-700/50 rounded-lg p-3 text-xs text-gray-400">
            <p className="font-semibold text-gray-300 mb-1">Mod Info</p>
            <p>Version: 1.6</p>
            <p>Author: Mehni</p>
            <p>License: MIT</p>
            <a
              href="https://github.com/Mehni/PickUpAndHaul"
              target="_blank"
              rel="noopener noreferrer"
              className="text-orange-400 hover:text-orange-300 mt-2 inline-block"
            >
              GitHub Repo →
            </a>
          </div>
        </div>
      )}
    </aside>
  );
}
