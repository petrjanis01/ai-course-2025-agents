import { format } from 'date-fns';
import { cs } from 'date-fns/locale';

export const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('cs-CZ', {
    style: 'currency',
    currency: 'CZK',
  }).format(amount);
};

export const formatDate = (dateString: string): string => {
  return format(new Date(dateString), 'dd.MM.yyyy', { locale: cs });
};

export const formatDateTime = (dateString: string): string => {
  return format(new Date(dateString), 'dd.MM.yyyy HH:mm', { locale: cs });
};
