# 12 Frontend - AI Chat s Agenty

## Cíl
Implementovat kompletní chat UI pro komunikaci s AI agenty s podporou conversation history a zobrazením metadat.

## Prerekvizity
- Dokončený krok 13 (Chat Agenti)
- Frontend setup (krok 04)

## Kroky implementace

### 1. Chat API service

**src/services/chat-service.ts:**
```typescript
import { apiClient } from './api-client';

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatRequest {
  message: string;
  sessionId?: string;
  conversationHistory?: ChatMessage[];
}

export interface ChatMetadata {
  tokensUsed: number;
  responseTime: number;
  agentsUsed: string[];
}

export interface ChatResponse {
  message: string;
  sessionId: string;
  metadata: ChatMetadata;
}

export const chatService = {
  async sendMessage(request: ChatRequest): Promise<ChatResponse> {
    const response = await apiClient.post<ChatResponse>('/api/chat/message', request);
    return response.data;
  }
};
```

### 2. Chat komponenta

**src/components/Chat/Chat.tsx:**
```tsx
import React, { useState, useRef, useEffect } from 'react';
import { chatService, ChatMessage, ChatMetadata } from '../../services/chat-service';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { Alert, AlertDescription } from '../ui/alert';
import { Badge } from '../ui/badge';
import { Separator } from '../ui/separator';
import {
  Send,
  Bot,
  User,
  Loader2,
  Trash2,
  Clock,
  Cpu,
  MessageSquare,
  AlertCircle
} from 'lucide-react';

export const Chat: React.FC = () => {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [sessionId, setSessionId] = useState<string | undefined>();
  const [lastMetadata, setLastMetadata] = useState<ChatMetadata | null>(null);
  const [error, setError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  const handleSend = async () => {
    if (!input.trim() || loading) return;

    const userMessage: ChatMessage = {
      role: 'user',
      content: input.trim()
    };

    setMessages((prev) => [...prev, userMessage]);
    setInput('');
    setLoading(true);
    setError(null);

    try {
      const response = await chatService.sendMessage({
        message: userMessage.content,
        sessionId,
        conversationHistory: messages
      });

      setSessionId(response.sessionId);
      setLastMetadata(response.metadata);

      const assistantMessage: ChatMessage = {
        role: 'assistant',
        content: response.message
      };

      setMessages((prev) => [...prev, assistantMessage]);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Chyba při zpracování zprávy');

      const errorMessage: ChatMessage = {
        role: 'assistant',
        content: 'Omlouvám se, došlo k chybě při zpracování vašeho dotazu. Zkuste to prosím znovu.'
      };

      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setLoading(false);
      inputRef.current?.focus();
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleClear = () => {
    if (window.confirm('Opravdu chcete smazat celou konverzaci?')) {
      setMessages([]);
      setSessionId(undefined);
      setLastMetadata(null);
      setError(null);
    }
  };

  const exampleQueries = [
    'Kolik transakcí bylo vytvořeno v roce 2025?',
    'Jaký byl celkový výdělek za tento měsíc?',
    'Která společnost má nejvíce transakcí?',
    'Najdi všechny faktury',
    'Které dokumenty zmiňují výpovědní lhůtu?'
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">AI Chat</h1>
          <p className="text-muted-foreground">
            Komunikujte s AI agenty o transakcích a dokumentech
          </p>
        </div>
        {messages.length > 0 && (
          <Button variant="outline" onClick={handleClear}>
            <Trash2 className="h-4 w-4 mr-2" />
            Smazat konverzaci
          </Button>
        )}
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Chat Window */}
        <div className="lg:col-span-2">
          <Card className="h-[600px] flex flex-col">
            <CardContent className="flex-1 flex flex-col p-0">
              {/* Messages */}
              <div className="flex-1 overflow-y-auto p-4 space-y-4">
                {messages.length === 0 ? (
                  <div className="text-center py-12">
                    <MessageSquare className="h-16 w-16 mx-auto mb-4 text-muted-foreground opacity-50" />
                    <h3 className="text-lg font-medium mb-2">Začněte konverzaci</h3>
                    <p className="text-sm text-muted-foreground mb-6">
                      Položte otázku o transakcích nebo dokumentech
                    </p>
                    <div className="space-y-2 max-w-md mx-auto">
                      <p className="text-xs text-muted-foreground font-medium">Příklady dotazů:</p>
                      {exampleQueries.map((query, index) => (
                        <button
                          key={index}
                          onClick={() => setInput(query)}
                          className="block w-full text-left text-sm p-2 rounded-md hover:bg-slate-100 transition-colors"
                        >
                          "{query}"
                        </button>
                      ))}
                    </div>
                  </div>
                ) : (
                  messages.map((message, index) => (
                    <div
                      key={index}
                      className={`flex gap-3 ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}
                    >
                      {message.role === 'assistant' && (
                        <div className="flex-shrink-0">
                          <div className="h-8 w-8 rounded-full bg-primary flex items-center justify-center">
                            <Bot className="h-5 w-5 text-primary-foreground" />
                          </div>
                        </div>
                      )}
                      <div
                        className={`max-w-[80%] rounded-lg p-3 ${
                          message.role === 'user'
                            ? 'bg-primary text-primary-foreground'
                            : 'bg-slate-100 text-slate-900'
                        }`}
                      >
                        <div className="text-xs font-semibold mb-1 opacity-70">
                          {message.role === 'user' ? 'Vy' : 'AI Asistent'}
                        </div>
                        <div className="whitespace-pre-wrap text-sm">{message.content}</div>
                      </div>
                      {message.role === 'user' && (
                        <div className="flex-shrink-0">
                          <div className="h-8 w-8 rounded-full bg-slate-300 flex items-center justify-center">
                            <User className="h-5 w-5 text-slate-700" />
                          </div>
                        </div>
                      )}
                    </div>
                  ))
                )}

                {loading && (
                  <div className="flex gap-3 justify-start">
                    <div className="flex-shrink-0">
                      <div className="h-8 w-8 rounded-full bg-primary flex items-center justify-center">
                        <Bot className="h-5 w-5 text-primary-foreground" />
                      </div>
                    </div>
                    <div className="bg-slate-100 rounded-lg p-3">
                      <div className="text-xs font-semibold mb-1 opacity-70">AI Asistent</div>
                      <div className="flex items-center gap-2 text-sm">
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Přemýšlím...
                      </div>
                    </div>
                  </div>
                )}

                <div ref={messagesEndRef} />
              </div>

              {/* Input */}
              <div className="p-4 border-t">
                <div className="flex gap-2">
                  <Input
                    ref={inputRef}
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyPress={handleKeyPress}
                    placeholder="Napište zprávu..."
                    disabled={loading}
                    className="flex-1"
                  />
                  <Button onClick={handleSend} disabled={loading || !input.trim()}>
                    {loading ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Send className="h-4 w-4" />
                    )}
                  </Button>
                </div>
                <p className="text-xs text-muted-foreground mt-2">
                  Stiskněte Enter pro odeslání
                </p>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Sidebar */}
        <div className="space-y-4">
          {/* Metadata */}
          {lastMetadata && (
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Poslední odpověď</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="flex items-center gap-2 text-sm">
                  <Clock className="h-4 w-4 text-muted-foreground" />
                  <span className="text-muted-foreground">Čas:</span>
                  <Badge variant="outline">
                    {lastMetadata.responseTime.toFixed(2)}s
                  </Badge>
                </div>
                {lastMetadata.agentsUsed && lastMetadata.agentsUsed.length > 0 && (
                  <div>
                    <div className="flex items-center gap-2 text-sm mb-2">
                      <Cpu className="h-4 w-4 text-muted-foreground" />
                      <span className="text-muted-foreground">Použité agenty:</span>
                    </div>
                    <div className="flex flex-wrap gap-1">
                      {lastMetadata.agentsUsed.map((agent, index) => (
                        <Badge key={index} variant="secondary" className="text-xs">
                          {agent}
                        </Badge>
                      ))}
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {/* Session Info */}
          {sessionId && (
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Session</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  <div>
                    <p className="text-xs text-muted-foreground">Session ID:</p>
                    <p className="text-xs font-mono bg-slate-100 p-2 rounded mt-1 break-all">
                      {sessionId}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">Počet zpráv:</p>
                    <p className="text-sm font-medium">{messages.length}</p>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Help */}
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Nápověda</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div>
                <h4 className="text-xs font-medium mb-2">Typy dotazů:</h4>
                <ul className="space-y-1 text-xs text-muted-foreground">
                  <li>• Databázové - statistiky, počty, sumy</li>
                  <li>• Dokumentové - vyhledávání v přílohách</li>
                  <li>• Kombinované - propojení obojího</li>
                </ul>
              </div>
              <Separator />
              <div>
                <h4 className="text-xs font-medium mb-2">Dostupné agenty:</h4>
                <div className="space-y-1">
                  <Badge variant="outline" className="text-xs">
                    DatabaseAgent
                  </Badge>
                  <p className="text-xs text-muted-foreground ml-1">
                    SQL dotazy, agregace
                  </p>
                </div>
                <div className="space-y-1 mt-2">
                  <Badge variant="outline" className="text-xs">
                    DocumentSearchAgent
                  </Badge>
                  <p className="text-xs text-muted-foreground ml-1">
                    Sémantické vyhledávání
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
};
```

### 3. Aktualizace routing

**src/App.tsx** - přidej route:
```tsx
import { Chat } from './components/Chat/Chat';

// V Routes:
<Route path="/chat" element={<Chat />} />

// V navigation (už by mělo být):
<Link
  to="/chat"
  className="text-sm font-medium hover:text-primary transition-colors"
>
  Chat
</Link>
```

## Testování

### 1. Základní konverzace

1. Otevři http://localhost:3000/chat
2. Zadej: "Kolik transakcí bylo vytvořeno v roce 2025?"
3. Ověř:
   - Odpověď se zobrazí
   - Metadata obsahují responseTime
   - Agent "DatabaseAgent" je uveden v použitých agentech

### 2. Multi-turn konverzace

1. První dotaz: "Kolik transakcí máme?"
2. Druhý dotaz: "A kolik z nich má přílohy?"
3. Ověř:
   - Druhá odpověď zohledňuje kontext první
   - Session ID zůstává stejné
   - Počet zpráv se zvyšuje

### 3. Dokumentové dotazy

1. Nejdříve nahraj dokumenty přes transakce
2. Zadej: "Najdi všechny faktury"
3. Ověř:
   - DocumentSearchAgent je použit
   - Odpověď obsahuje informace z dokumentů

### 4. Příklady dotazů

Klikni na příklady dotazů pod prázdným chatem:
- Ověř, že se query automaticky vyplní do inputu

### 5. Clear konverzace

1. Vytvoř několik zpráv
2. Klikni "Smazat konverzaci"
3. Potvrď dialog
4. Ověř:
   - Všechny zprávy jsou smazány
   - Session ID je resetován
   - Zobrazí se úvodní obrazovka

## Ověření

Po dokončení:

1. ✅ Chat rozhraní se správně zobrazuje
2. ✅ Zprávy uživatele a asistenta mají odlišný styl
3. ✅ Loading state při čekání na odpověď
4. ✅ Metadata (responseTime, agentsUsed) se zobrazují
5. ✅ Session ID je persistentní během konverzace
6. ✅ Multi-turn konverzace funguje (history)
7. ✅ Příklady dotazů jsou klikatelné
8. ✅ Clear konverzace funguje
9. ✅ Enter odesílá zprávu
10. ✅ Error handling zobrazuje chyby

## Výstup této fáze

✅ Kompletní chat UI s moderním designem
✅ Conversation history support
✅ Zobrazení response metadata
✅ Session management
✅ Příklady dotazů pro rychlý start
✅ Rozlišení user/assistant zpráv
✅ Loading states a error handling
✅ Sidebar s informacemi o session
✅ Help section s nápovědou

## Další krok

→ **13_langfuse_monitoring.md** - Integrace Langfuse pro monitoring LLM calls
