import React from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import { TransactionsPage } from './pages/TransactionsPage';
import { TransactionDetail } from './components/TransactionDetail';
import { DataManagement } from './components/DataManagement/DataManagement';
import { QdrantTest } from './components/QdrantTest/QdrantTest';
import { Chat } from './components/Chat/Chat';

function App() {
  return (
    <Router>
      <div className="min-h-screen bg-background">
        <header className="border-b">
          <div className="container mx-auto px-4 py-4">
            <div className="flex items-center justify-between">
              <Link to="/" className="text-2xl font-bold">
                Transaction Management
              </Link>
              <nav className="flex gap-4">
                <Link
                  to="/"
                  className="text-sm font-medium hover:text-primary transition-colors"
                >
                  Transakce
                </Link>
                <Link
                  to="/chat"
                  className="text-sm font-medium hover:text-primary transition-colors"
                >
                  AI Chat
                </Link>
                <Link
                  to="/data"
                  className="text-sm font-medium hover:text-primary transition-colors"
                >
                  Správa dat
                </Link>
                <Link
                  to="/qdrant-test"
                  className="text-sm font-medium hover:text-primary transition-colors"
                >
                  Test Qdrant
                </Link>
              </nav>
            </div>
          </div>
        </header>
        <main className="container mx-auto px-4 py-8">
          <Routes>
            <Route path="/" element={<TransactionsPage />} />
            <Route path="/transactions/:id" element={<TransactionDetail />} />
            <Route path="/chat" element={<Chat />} />
            <Route path="/data" element={<DataManagement />} />
            <Route path="/qdrant-test" element={<QdrantTest />} />
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;
