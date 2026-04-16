import { formatDate } from '@angular/common';

const IST_TIME_ZONE = 'Asia/Kolkata';
const DATE_WITH_OFFSET_REGEX = /(?:Z|[+-]\d{2}:?\d{2})$/i;

const istDatePartsFormatter = new Intl.DateTimeFormat('en-CA', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  timeZone: IST_TIME_ZONE
});

export type DateInput = string | number | Date | null | undefined;

export function parseUtcDate(value: DateInput): Date {
  if (value instanceof Date) return new Date(value.getTime());
  if (typeof value === 'number') return new Date(value);

  if (typeof value !== 'string') {
    return new Date(Number.NaN);
  }

  const normalized = value.trim().replace(' ', 'T');
  if (!normalized) return new Date(Number.NaN);

  const hasOffset = DATE_WITH_OFFSET_REGEX.test(normalized);
  return new Date(hasOffset ? normalized : `${normalized}Z`);
}

export function getUtcTimestamp(value: DateInput): number {
  const date = parseUtcDate(value);
  const timestamp = date.getTime();
  return Number.isNaN(timestamp) ? 0 : timestamp;
}

export function formatIstDate(value: DateInput, pattern: string, fallback = '-'): string {
  const date = parseUtcDate(value);
  if (Number.isNaN(date.getTime())) return fallback;

  return formatDate(date, pattern, 'en-IN', IST_TIME_ZONE);
}

export function getIstEpochDay(value: DateInput): number {
  const date = parseUtcDate(value);
  if (Number.isNaN(date.getTime())) return Number.NaN;

  const parts = istDatePartsFormatter.formatToParts(date);
  const year = Number(parts.find((part) => part.type === 'year')?.value);
  const month = Number(parts.find((part) => part.type === 'month')?.value);
  const day = Number(parts.find((part) => part.type === 'day')?.value);

  return Math.floor(Date.UTC(year, month - 1, day) / (1000 * 60 * 60 * 24));
}
