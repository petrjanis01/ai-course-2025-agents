# 14 Frontend - Document Generator

## Cíl
Implementovat UI pro generování vzorových Markdown dokumentů (faktury, smlouvy, objednávky) s výběrem varianty.

## Prerekvizity
- Dokončený krok 16 (Document Generator API)
- Frontend setup (krok 04)

## Kroky implementace

### 1. Document Generator API service

**src/services/document-generator-service.ts:**
```typescript
import { apiClient } from './api-client';

export type DocumentType = 'invoice' | 'contract' | 'purchase_order';

export interface GenerateDocumentRequest {
  documentType: DocumentType;
  variant: number; // 1, 2, or 3
  customData?: Record<string, string>;
}

export const documentGeneratorService = {
  async generateDocument(request: GenerateDocumentRequest): Promise<Blob> {
    const response = await apiClient.post('/api/documents/generate', request, {
      responseType: 'blob'
    });
    return response.data;
  }
};
```

### 2. Document Generator komponenta

**src/components/DocumentGenerator/DocumentGenerator.tsx:**
```tsx
import React, { useState } from 'react';
import { documentGeneratorService, DocumentType } from '../../services/document-generator-service';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';
import { Button } from '../ui/button';
import { Label } from '../ui/label';
import { Input } from '../ui/input';
import { Alert, AlertDescription } from '../ui/alert';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';
import { Separator } from '../ui/separator';
import {
  FileText,
  Download,
  Loader2,
  AlertCircle,
  CheckCircle2,
  FileCode,
  FileContract,
  ShoppingCart
} from 'lucide-react';

export const DocumentGenerator: React.FC = () => {
  const [documentType, setDocumentType] = useState<DocumentType>('invoice');
  const [variant, setVariant] = useState<number>(1);
  const [customData, setCustomData] = useState<Record<string, string>>({});
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const documentTypes: Array<{ value: DocumentType; label: string; icon: React.ReactNode; description: string }> = [
    {
      value: 'invoice',
      label: 'Faktura',
      icon: <FileText className="h-5 w-5" />,
      description: 'Daňový doklad s položkami a DPH'
    },
    {
      value: 'contract',
      label: 'Smlouva',
      icon: <FileContract className="h-5 w-5" />,
      description: 'Rámcová smlouva o dodávkách'
    },
    {
      value: 'purchase_order',
      label: 'Objednávka',
      icon: <ShoppingCart className="h-5 w-5" />,
      description: 'Kupní objednávka se specifikací'
    }
  ];

  const variants = [
    { value: 1, label: 'Varianta 1', description: 'Základní formát' },
    { value: 2, label: 'Varianta 2', description: 'Tabulkový formát' },
    { value: 3, label: 'Varianta 3', description: 'Rozšířený formát' }
  ];

  const customFields = [
    { key: 'SupplierName', label: 'Název dodavatele', placeholder: 'ACME Corporation s.r.o.' },
    { key: 'CustomerName', label: 'Název odběratele', placeholder: 'Test Client Ltd.' },
    { key: 'Amount', label: 'Částka (Kč)', placeholder: '50000', type: 'number' },
  ];

  const handleGenerate = async () => {
    try {
      setGenerating(true);
      setError(null);
      setSuccess(null);

      const blob = await documentGeneratorService.generateDocument({
        documentType,
        variant,
        customData: Object.keys(customData).length > 0 ? customData : undefined
      });

      // Download the file
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${documentType}_variant_${variant}_${Date.now()}.md`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);

      setSuccess('Dokument úspěšně vygenerován a stažen');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Chyba při generování dokumentu');
    } finally {
      setGenerating(false);
    }
  };

  const handleCustomDataChange = (key: string, value: string) => {
    setCustomData(prev => {
      if (!value) {
        const { [key]: _, ...rest } = prev;
        return rest;
      }
      return { ...prev, [key]: value };
    });
  };

  const selectedDocType = documentTypes.find(dt => dt.value === documentType);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Generátor dokumentů</h1>
        <p className="text-muted-foreground">
          Vygenerujte vzorové Markdown dokumenty pro testování
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {success && (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertDescription>{success}</AlertDescription>
        </Alert>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Configuration */}
        <div className="lg:col-span-2 space-y-6">
          {/* Document Type */}
          <Card>
            <CardHeader>
              <CardTitle>Typ dokumentu</CardTitle>
              <CardDescription>Vyberte typ dokumentu, který chcete vygenerovat</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                {documentTypes.map((type) => (
                  <button
                    key={type.value}
                    onClick={() => setDocumentType(type.value)}
                    className={`p-4 rounded-lg border-2 transition-all ${
                      documentType === type.value
                        ? 'border-primary bg-primary/5'
                        : 'border-border hover:border-primary/50'
                    }`}
                  >
                    <div className="flex flex-col items-center text-center gap-2">
                      {type.icon}
                      <div>
                        <div className="font-medium">{type.label}</div>
                        <div className="text-xs text-muted-foreground mt-1">
                          {type.description}
                        </div>
                      </div>
                    </div>
                  </button>
                ))}
              </div>
            </CardContent>
          </Card>

          {/* Variant */}
          <Card>
            <CardHeader>
              <CardTitle>Varianta šablony</CardTitle>
              <CardDescription>Každý typ dokumentu má 3 různé varianty formátování</CardDescription>
            </CardHeader>
            <CardContent>
              <RadioGroup
                value={variant.toString()}
                onValueChange={(value) => setVariant(parseInt(value))}
              >
                <div className="space-y-3">
                  {variants.map((v) => (
                    <div
                      key={v.value}
                      className="flex items-center space-x-3 p-3 rounded-lg border hover:bg-slate-50 transition-colors"
                    >
                      <RadioGroupItem value={v.value.toString()} id={`variant-${v.value}`} />
                      <Label
                        htmlFor={`variant-${v.value}`}
                        className="flex-1 cursor-pointer"
                      >
                        <div className="font-medium">{v.label}</div>
                        <div className="text-sm text-muted-foreground">{v.description}</div>
                      </Label>
                    </div>
                  ))}
                </div>
              </RadioGroup>
            </CardContent>
          </Card>

          {/* Custom Data */}
          <Card>
            <CardHeader>
              <CardTitle>Vlastní data (volitelné)</CardTitle>
              <CardDescription>
                Přepište výchozí náhodná data vlastními hodnotami
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {customFields.map((field) => (
                  <div key={field.key} className="space-y-2">
                    <Label htmlFor={field.key}>{field.label}</Label>
                    <Input
                      id={field.key}
                      type={field.type || 'text'}
                      placeholder={field.placeholder}
                      value={customData[field.key] || ''}
                      onChange={(e) => handleCustomDataChange(field.key, e.target.value)}
                    />
                  </div>
                ))}
                <p className="text-xs text-muted-foreground">
                  Nevyplněná pole budou nahrazena náhodnými realistickými daty
                </p>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Preview & Actions */}
        <div className="space-y-4">
          {/* Preview */}
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Náhled konfigurace</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div>
                <p className="text-xs text-muted-foreground">Typ dokumentu:</p>
                <div className="flex items-center gap-2 mt-1">
                  {selectedDocType?.icon}
                  <p className="font-medium">{selectedDocType?.label}</p>
                </div>
              </div>
              <Separator />
              <div>
                <p className="text-xs text-muted-foreground">Varianta:</p>
                <p className="font-medium">{variants.find(v => v.value === variant)?.label}</p>
              </div>
              {Object.keys(customData).length > 0 && (
                <>
                  <Separator />
                  <div>
                    <p className="text-xs text-muted-foreground mb-2">Vlastní data:</p>
                    <div className="space-y-1">
                      {Object.entries(customData).map(([key, value]) => (
                        <div key={key} className="text-xs">
                          <span className="font-mono text-muted-foreground">{key}:</span>{' '}
                          <span className="font-medium">{value}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          {/* Action Button */}
          <Button
            onClick={handleGenerate}
            disabled={generating}
            className="w-full"
            size="lg"
          >
            {generating ? (
              <>
                <Loader2 className="h-5 w-5 mr-2 animate-spin" />
                Generuji...
              </>
            ) : (
              <>
                <Download className="h-5 w-5 mr-2" />
                Vygenerovat a stáhnout
              </>
            )}
          </Button>

          {/* Info */}
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Informace</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 text-xs text-muted-foreground">
              <p>
                • Dokument bude vygenerován ve formátu Markdown (.md)
              </p>
              <p>
                • Obsahuje realistická testovací data
              </p>
              <p>
                • Můžete použít pro testování procesu nahrávání příloh
              </p>
              <p>
                • Každá varianta má jiný formát a strukturu
              </p>
            </CardContent>
          </Card>

          {/* Quick Actions */}
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Rychlé akce</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <Button
                variant="outline"
                size="sm"
                className="w-full justify-start"
                onClick={() => {
                  setDocumentType('invoice');
                  setVariant(1);
                  setCustomData({});
                }}
              >
                <FileText className="h-4 w-4 mr-2" />
                Faktura (v1)
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="w-full justify-start"
                onClick={() => {
                  setDocumentType('contract');
                  setVariant(1);
                  setCustomData({});
                }}
              >
                <FileContract className="h-4 w-4 mr-2" />
                Smlouva (v1)
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="w-full justify-start"
                onClick={() => {
                  setDocumentType('purchase_order');
                  setVariant(1);
                  setCustomData({});
                }}
              >
                <ShoppingCart className="h-4 w-4 mr-2" />
                Objednávka (v1)
              </Button>
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
import { DocumentGenerator } from './components/DocumentGenerator/DocumentGenerator';

// V Routes:
<Route path="/generate-document" element={<DocumentGenerator />} />

// V navigation:
<Link
  to="/generate-document"
  className="text-sm font-medium hover:text-primary transition-colors"
>
  Generátor dokumentů
</Link>
```

### 4. Přidání RadioGroup komponenty (pokud chybí)

```bash
npx shadcn@latest add radio-group
```

## Testování

### 1. Vygeneruj fakturu (varianta 1)

1. Otevři http://localhost:3000/generate-document
2. Vyber "Faktura"
3. Ponechej variantu 1
4. Klikni "Vygenerovat a stáhnout"
5. Ověř:
   - Soubor se stáhne (`invoice_variant_1_*.md`)
   - Success zpráva se zobrazí
   - Otevři soubor a zkontroluj obsah

### 2. Vygeneruj smlouvu s vlastními daty

1. Vyber "Smlouva"
2. Vyber variantu 2
3. Vyplň vlastní data:
   - Název dodavatele: "My Company s.r.o."
   - Částka: 100000
4. Klikni "Vygenerovat a stáhnout"
5. Ověř:
   - Vlastní data jsou ve vygenerovaném souboru
   - Ostatní pole mají náhodná data

### 3. Rychlé akce

1. Klikni na "Faktura (v1)" v rychlých akcích
2. Ověř:
   - Typ se nastaví na faktura
   - Varianta se nastaví na 1
   - Custom data se vymažou

### 4. Všechny varianty

Vygeneruj všechny 3 varianty každého typu (celkem 9 souborů):
1. Invoice - varianty 1, 2, 3
2. Contract - varianty 1, 2, 3
3. Purchase Order - varianty 1, 2, 3

Ověř, že každá varianta má jiné formátování.

### 5. Test upload

1. Vygeneruj dokument
2. Přejdi na detail nějaké transakce
3. Nahraj vygenerovaný MD soubor jako přílohu
4. Ověř, že se nahraje a zpracuje

## Ověření

Po dokončení:

1. ✅ UI pro výběr typu dokumentu funguje
2. ✅ Výběr varianty (1-3) funguje
3. ✅ Custom data fields fungují
4. ✅ Generování dokumentu funguje
5. ✅ Soubor se automaticky stahuje
6. ✅ Náhled konfigurace je správný
7. ✅ Rychlé akce fungují
8. ✅ Success/Error hlášky se zobrazují
9. ✅ Vygenerované dokumenty mají správný obsah
10. ✅ Loading state při generování

## Výstup této fáze

✅ DocumentGenerator komponenta s full UI
✅ Výběr typu dokumentu (faktura, smlouva, objednávka)
✅ Výběr varianty šablony (1-3)
✅ Custom data override
✅ Automatické stahování vygenerovaných souborů
✅ Náhled konfigurace
✅ Rychlé akce pro časté použití
✅ Info sekce s nápovědou

## Další krok

→ **15_docker_compose.md** - Kompletní Docker Compose setup pro celou aplikaci
