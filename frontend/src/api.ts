import axios from 'axios';

export interface Transaction {
  id?: number;
  description: string;
  amount: number;
  category: string;
  date?: string;
  merchant: string;
}

const API_URL = import.meta.env.VITE_API_URL || 'https://app-vaultflow-api.wittygrass-3fa04590.westus3.azurecontainerapps.io/api';

export const getTransactions = async (): Promise<Transaction[]> => {
  const response = await axios.get<Transaction[]>(`${API_URL}/transactions`);
  return response.data;
};

export const createTransaction = async (transaction: Transaction): Promise<Transaction> => {
  const response = await axios.post<Transaction>(`${API_URL}/transactions`, transaction);
  return response.data;
};