export interface CardIssuer {
  id: string;
  name: string;
  network: number; // Enum: Visa=1, Mastercard=2, etc.
  logoUrl?: string;
  colorHex?: string;
}

export interface CreditCard {
  id: string;
  cardholderName: string;
  last4: string;
  expMonth: number;
  expYear: number;
  issuerId: string;
  issuerName: string;
  network: string;
  isDefault: boolean;
  creditLimit: number;
  outstandingBalance: number;
  availableCredit: number;
}

export enum TransactionType {
  Purchase = 1,
  Payment = 2,
  Refund = 3
}

export interface CardTransaction {
  id: string;
  cardId: string;
  userId: string;
  type: TransactionType;
  amount: number;
  description: string;
  dateUtc: string;
}

export interface CreateCardRequest {
  cardholderName: string;
  expMonth: number;
  expYear: number;
  cardNumber: string;
  issuerId: string;
  isDefault: boolean;
}
